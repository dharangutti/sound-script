import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { identity, validatePublication, matchIdentity, promote, guardChanges, existingPromotion } from './release-promotion.mjs';

const root = process.cwd();
const command = (name, args, options = {}) => execFileSync(name, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'], ...options });
const git = (...args) => command('git', args).trim();
const gh = (...args) => command('gh', args);
const read = p => fs.readFileSync(p, 'utf8').replace(/^\ufeff/, '');
const output = (key, value) => {
    if (/\r|\n/.test(String(value))) throw new Error(`Invalid multiline output ${key}`);
    if (process.env.GITHUB_OUTPUT) fs.appendFileSync(process.env.GITHUB_OUTPUT, `${key}=${value}\n`);
    console.log(`${key}: ${value}`);
};
function changes() {
    const status = command('git', ['status', '--porcelain=v1', '-z']).split('\0').filter(Boolean);
    const files = status.map(line => {
        if (!/^ M /.test(line)) throw new Error(`Unexpected promotion status: ${line}`);
        return line.slice(3);
    });
    guardChanges(files);
    console.log(`Release promotion changed:\n${files.map(p => `  ${p}`).join('\n') || '  (none)'}`);
    return files;
}
function assertClean() {
    if (git('status', '--porcelain')) throw new Error('Promotion requires a clean checkout.');
}
function validate() {
    command('pwsh', ['-NoProfile', '-File', 'scripts/update-docs.ps1', '-Check'], { stdio: 'inherit' });
    command('node', ['--test', 'scripts/docs-state.test.mjs', 'scripts/homepage-release.test.cjs', 'scripts/release-promotion.test.mjs'], { stdio: 'inherit' });
    // Homepage release history belongs to the staged site, never the source template.
    const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-homepage-'));
    try {
        fs.copyFileSync('docs/index.html', path.join(stage, 'index.html'));
        command('pwsh', ['-NoProfile', '-File', 'scripts/update-homepage-release.ps1', '-IndexPath', path.join(stage, 'index.html')], { stdio: 'inherit' });
        const html = read(path.join(stage, 'index.html'));
        const label = `V${JSON.parse(read('docs/release-state.json')).publicVersion.split('.')[0]}`;
        if (!html.includes('release-current') || !html.includes(`>${label}</span>`) || !html.includes('>Current</span>')) throw new Error('Staged homepage missing current release.');
    } finally { fs.rmSync(stage, { recursive: true, force: true }); }
    command('git', ['diff', '--check'], { stdio: 'inherit' });
}

async function main() {
    const mode = process.argv[2];
    if (mode === 'resolve') {
        const repository = process.env.GITHUB_REPOSITORY;
        const id = process.env.PUBLICATION_RUN_ID;
        if (!/^\d+$/.test(id ?? '') || !repository) throw new Error('A numeric publication_run_id is required.');
        const run = JSON.parse(gh('api', `repos/${repository}/actions/runs/${id}`));
        const pages = JSON.parse(gh('api', '--paginate', '--slurp', `repos/${repository}/actions/runs/${id}/attempts/${run.run_attempt}/jobs?per_page=100`));
        const sha = validatePublication(run, pages.flatMap(page => page.jobs), repository);
        const xml = gh('api', '-H', 'Accept: application/vnd.github.raw+json', `repos/${repository}/contents/Directory.Build.props?ref=${sha}`);
        const published = identity(xml);
        matchIdentity(published, identity(read('Directory.Build.props')));
        output('version', published.version);
        output('publication_sha', sha);
        output('base_sha', git('rev-parse', 'HEAD'));
        output('publication_url', run.html_url);
        output('run_id', id);
        console.log(`Development version: ${published.version}\nVersion label: ${published.label}\nCodename: ${published.codename}`);
    } else if (mode === 'prepare') {
        assertClean();
        const { RELEASE_VERSION: version, PUBLICATION_SHA: sha } = process.env;
        if (!/^[a-f0-9]{40}$/.test(sha ?? '')) throw new Error('Invalid publication SHA.');
        const published = identity(command('git', ['show', `${sha}:Directory.Build.props`]));
        if (published.version !== version) throw new Error('Wrong publication version.');
        const current = identity(read('Directory.Build.props'));
        matchIdentity(published, current);
        command('pwsh', ['-NoProfile', '-File', 'scripts/verify-public-nuget.ps1', '-Version', version], { stdio: 'inherit' });
        const state = JSON.parse(read('docs/release-state.json'));
        const notes = read('RELEASE_NOTES.md');
        const result = promote(state, notes, published, current);
        // An already-promoted release must be clean; do not silently repair drift on reruns.
        if (state.publicVersion === version) {
            if (JSON.stringify(result.state) !== JSON.stringify(state) || result.notes !== notes) throw new Error('Already-promoted release has inconsistent notes/channel state. Repair explicitly.');
            validate();
            changes();
            output('changed', 'false');
            return;
        }
        fs.writeFileSync('docs/release-state.json', JSON.stringify(result.state, null, 2) + '\n');
        fs.writeFileSync('RELEASE_NOTES.md', result.notes);
        command('pwsh', ['-NoProfile', '-File', 'scripts/update-docs.ps1'], { stdio: 'inherit' });
        validate();
        const files = changes();
        const artifact = path.join(root, 'artifacts', 'promotion');
        fs.mkdirSync(artifact, { recursive: true });
        fs.writeFileSync(path.join(artifact, 'promotion.patch'), command('git', ['diff', '--binary', '--no-ext-diff']));
        output('changed', String(files.length > 0));
    } else if (mode === 'pr') {
        assertClean();
        const version = identity(read('Directory.Build.props')).version;
        if (version !== process.env.RELEASE_VERSION || git('rev-parse', 'HEAD') !== process.env.BASE_SHA) throw new Error('Main changed during verification. Rerun promotion against current main.');
        const branch = `automation/promote-${version}`;
        const prs = JSON.parse(gh('pr', 'list', '--head', branch, '--base', 'main', '--state', 'open', '--json', 'headRefName,baseRefName,state,url'));
        const existing = existingPromotion(prs, branch);
        if (existing) { console.log(`Promotion PR already exists; review it or close it before regenerating: ${existing.url}`); return; }
        if (git('ls-remote', '--heads', 'origin', branch)) throw new Error(`Branch ${branch} already exists without an open PR; inspect/remove it before retrying. No branch was overwritten.`);
        command('git', ['apply', '--check', process.env.PROMOTION_PATCH]);
        command('git', ['apply', process.env.PROMOTION_PATCH]);
        const files = changes();
        if (!files.length) { console.log('No promotion changes; no PR needed.'); return; }
        git('switch', '-c', branch);
        git('config', 'user.name', 'github-actions[bot]');
        git('config', 'user.email', '41898282+github-actions[bot]@users.noreply.github.com');
        git('add', '--', ...files);
        const title = `Promote SoundScript ${version} to public release`;
        git('commit', '-m', title);
        // Recheck immediately before publication; never force-push a promotion branch.
        if (git('ls-remote', 'origin', 'refs/heads/main').split(/\s/)[0] !== process.env.BASE_SHA) throw new Error('Main advanced before PR creation; rerun promotion.');
        git('push', 'origin', branch);
        const body = `Promotes verified SoundScript ${version} through the existing documentation generator.\n\nPublication: ${process.env.PUBLICATION_URL}\nPublication commit: ${process.env.PUBLICATION_SHA}\n\n` +
            ['NuGet public availability', 'Fresh net10.0 consumer restore', 'Consumer build', 'Public API smoke test (WAV/MIDI/scene/JSON/SVG)', 'Release-state update', 'Release-notes promotion', 'Documentation generation', 'Documentation drift check', 'Homepage regression and staging', 'Changed-file guard'].map(item => `- ${item}: PASS`).join('\n') +
            '\n\nReview and merge after required PR checks pass. CLI distribution flags are version-specific. This PR does not publish packages or tags.\n';
        const bodyPath = path.join(os.tmpdir(), `promotion-body-${process.pid}.md`);
        fs.writeFileSync(bodyPath, body);
        try { console.log(gh('pr', 'create', '--base', 'main', '--head', branch, '--title', title, '--body-file', bodyPath)); }
        finally { fs.unlinkSync(bodyPath); }
    } else throw new Error('Usage: promote-public-release.mjs resolve|prepare|pr');
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
