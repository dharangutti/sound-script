import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { identity, validatePublication, matchIdentity, waitForNuget, promote, allowedFiles, guardChanges, existingPromotion } from './release-promotion.mjs';
import { facts, render, validateCurrent, inventory, run, repo } from './docs-state.mjs';

const release = version => ({ version, label: `V${version.split('.')[0]}`, codename: 'Release testing' });
const state = version => ({ schemaVersion: 1, publicVersion: version, library: { nugetPublished: true }, cli: { nugetPublished: false, githubReleasePublished: true } });
const notes = r => `# Notes\n\n## ${r.version} — ${r.codename} (unreleased)\n\n- Changes.\n\n## 13.0.0 — Older (unreleased)\n\n- Historical.\n`;
for (const version of ['15.0.0', '16.0.0', '17.2.1']) {
    test(`promotion and dynamic current-public validation for ${version}`, () => {
        const r = release(version), previous = `${Number(version.split('.')[0]) - 1}.0.0`;
        const result = promote(state(previous), notes(r), r, r);
        assert.equal(result.state.publicVersion, version);
        assert.deepEqual(result.state.cli, { nugetPublished: false, githubReleasePublished: false });
        assert.ok(result.notes.includes('## 13.0.0 — Older (unreleased)'));
        assert.ok(!result.notes.includes(`${r.codename} (unreleased)`));
        assert.deepEqual(promote(result.state, result.notes, r, r), result);
        result.state.cli = { nugetPublished: true, githubReleasePublished: true };
        assert.deepEqual(promote(result.state, result.notes, r, r).state.cli, result.state.cli);
        const f = structuredClone(facts(repo));
        f.development = `${Number(version.split('.')[0]) + 1}.0.0`; f.label = `V${f.development.split('.')[0]}`; f.state = result.state;
        assert.ok(render('LIBRARY_INSTALL', f).includes(`--version ${version}`));
        assert.ok(!render('LIBRARY_INSTALL', f).includes(`--version ${f.development}`));
        assert.ok(render('CLI_DISTRIBUTION', f).includes(`SoundScript.Cli --version ${version}`));
        assert.throws(() => validateCurrent(`The ${r.label} candidate package is ready`, 'test.md', f), /CANDIDATE/);
        assert.doesNotThrow(() => validateCurrent(`V${Number(version.split('.')[0]) - 1} candidate was historical.`, 'test.md', f));
    });
}
test('identity parses canonical properties and rejects invalid identity', () => {
    assert.equal(identity(fs.readFileSync(path.join(repo, 'Directory.Build.props'), 'utf8')).version, facts(repo).development);
    const xml = '<Project><PropertyGroup><Version>16.0.0</Version><SoundScriptVersionLabel>V16</SoundScriptVersionLabel><SoundScriptCodename>A &amp; B</SoundScriptCodename></PropertyGroup></Project>';
    assert.equal(identity(xml).codename, 'A & B');
    for (const bad of [xml.replace('16.0.0', '16.0'), xml.replace('V16', 'V15'), xml.replace('<PropertyGroup>', '<PropertyGroup Condition="x">'), xml.replace('</Version>', '</Version><Version>16.0.0</Version>')]) assert.throws(() => identity(bad));
});
test('release headings and publication/current mismatch fail before mutation', () => {
    const r = release('15.0.0'), s = state('14.0.0'), original = structuredClone(s);
    for (const text of ['', notes(r) + notes(r), notes(r).replace('Release testing', 'Wrong title')]) assert.throws(() => promote(s, text, r, r), /heading/);
    assert.throws(() => promote(s, notes(r), r, release('16.0.0')), /main may have advanced/);
    assert.throws(() => matchIdentity(release('14.0.0'), r), /does not match/);
    assert.deepEqual(s, original);
});
const repository = 'owner/repo';
const publication = () => ({ repository: { full_name: repository }, head_repository: { full_name: repository }, path: '.github/workflows/publish-nuget.yml', head_branch: 'main', event: 'workflow_dispatch', conclusion: 'success', head_sha: 'a'.repeat(40) });
const jobs = [{ steps: [{ name: 'Publish to nuget.org', conclusion: 'success' }] }];
test('publication requires correct repository, workflow, branch, result and actual publish step', () => {
    assert.equal(validatePublication(publication(), jobs, repository), 'a'.repeat(40));
    for (const field of ['head_branch', 'conclusion', 'path', 'event', 'head_sha']) {
        const run = publication(); run[field] = 'wrong'; assert.throws(() => validatePublication(run, jobs, repository));
    }
    const fork = publication(); fork.head_repository.full_name = 'fork/repo'; assert.throws(() => validatePublication(fork, jobs, repository));
    for (const conclusion of ['skipped', 'failure', 'cancelled']) assert.throws(() => validatePublication(publication(), [{ steps: [{ name: 'Publish to nuget.org', conclusion }] }], repository), /Publish step/);
    assert.throws(() => validatePublication(publication(), [], repository), /Publish step/);
});
test('NuGet polling retries and stops within a bounded interval without networking', async () => {
    let clock = 0, attempts = 0;
    const options = { now: () => clock, sleep: async ms => { clock += ms; }, timeout: 60000 };
    await waitForNuget('16.0.0', { ...options, fetchIndex: async () => ({ versions: ++attempts === 2 ? ['16.0.0'] : ['15.0.0'] }) });
    assert.equal(attempts, 2); assert.equal(clock, 30000);
    clock = 0; attempts = 0;
    await assert.rejects(waitForNuget('16.0.0', { ...options, fetchIndex: async () => { attempts++; throw new Error('unavailable'); } }), /rerun promotion after NuGet indexing/);
    assert.equal(clock, 60000); assert.equal(attempts, 2);
    await assert.rejects(waitForNuget('bad', options), /Invalid/);
});
test('exact diff allowlist and duplicate PR prevention', () => {
    assert.deepEqual(guardChanges(['README.md', 'packaging/README.md']), ['README.md', 'packaging/README.md']);
    for (const p of ['src/SoundScript/Engine.cs', 'docs/unexpected.md', '.github/workflows/tests.yml']) assert.throws(() => guardChanges([p]), /Unexpected/);
    const pr = { headRefName: 'automation/promote-16.0.0', baseRefName: 'main', state: 'OPEN', url: 'example' };
    assert.equal(existingPromotion([pr], pr.headRefName), pr);
    assert.equal(existingPromotion([{ ...pr, state: 'CLOSED' }], pr.headRefName), null);
    assert.equal(existingPromotion([], pr.headRefName), null);
    assert.throws(() => existingPromotion([pr, pr], pr.headRefName), /Multiple/);
});
test('future promotion uses real generator, exact allowlist and clean second run', t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-promotion-test-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    // Minimal coherent repository with the real generated blocks and CLI contract.
    for (const p of ['Directory.Build.props', 'src/SoundScript/SoundScript.csproj', 'src/SoundScript.Cli/SoundScript.Cli.csproj', 'src/SoundScript.Cli/CliArguments.cs', 'src/SoundScript.Cli/Diagnostics.cs', 'src/SoundScript.Cli/Program.cs']) {
        fs.mkdirSync(path.dirname(path.join(root, p)), { recursive: true });
        fs.copyFileSync(path.join(repo, p), path.join(root, p));
    }
    const propsPath = path.join(root, 'Directory.Build.props');
    let props = fs.readFileSync(propsPath, 'utf8');
    props = props.replace(/<Version>[^<]+/, '<Version>16.0.0').replace(/<SoundScriptVersionLabel>[^<]+/, '<SoundScriptVersionLabel>V16').replace(/<SoundScriptCodename>[^<]+/, '<SoundScriptCodename>Release testing');
    fs.writeFileSync(propsPath, props);
    const r = release('16.0.0');
    const write = (p, value) => { fs.mkdirSync(path.dirname(path.join(root, p)), { recursive: true }); fs.writeFileSync(path.join(root, p), value); };
    const cli = fs.readFileSync(path.join(repo, 'docs/cli.md'), 'utf8');
    write('docs/cli.md', cli.slice(cli.indexOf('## Public command coverage')));
    write('docs/documentation.md', '# Documentation\n[CLI](cli.md)\n');
    write('docs/SoundScript.md', '# Compatibility\n[Hub](documentation.md)\n');
    write('docs/doc.html', "var DEFAULT_PAGE = 'documentation.md';");
    write('docs/release-state.json', JSON.stringify(state('15.0.0')));
    write('RELEASE_NOTES.md', notes(r));
    const generatedBlocks = { 'README.md': ['LIBRARY_INSTALL', 'CURRENT_PUBLIC_RELEASE'], 'packaging/README.md': ['LIBRARY_INSTALL'], 'docs/index.html': ['PUBLIC_BADGE'] };
    for (const [p, ids] of Object.entries(generatedBlocks)) write(p, ids.map(id => `<!-- GENERATED:${id}_START -->\nold\n<!-- GENERATED:${id}_END -->`).join('\n'));
    write('docs/docs-manifest.json', '{}');
    write('docs/docs-manifest.json', JSON.stringify({ schemaVersion: 1, livingDocuments: inventory(root).filter(p => p !== 'RELEASE_NOTES.md'), historicalDocuments: ['RELEASE_NOTES.md'], historicalPatterns: [], generatedOnly: [], excluded: {}, generatedBlocks, classificationOverrides: {} }));
    const notesPath = path.join(root, 'RELEASE_NOTES.md');
    run(root); // Generate the new development identity before measuring public promotion.
    const result = promote(JSON.parse(fs.readFileSync(path.join(root, 'docs/release-state.json'))), fs.readFileSync(notesPath, 'utf8'), r, r);
    fs.writeFileSync(path.join(root, 'docs/release-state.json'), JSON.stringify(result.state));
    fs.writeFileSync(notesPath, result.notes);
    const changed = run(root).changed;
    assert.ok(changed.length > 0);
    assert.ok(changed.every(p => allowedFiles.has(p)));
    assert.deepEqual(run(root).changed, []);
    assert.deepEqual(run(root, true).changed, []);
});
