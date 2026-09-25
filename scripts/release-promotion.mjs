import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { semver, compareVersions, validateState } from './docs-state.mjs';

export function identity(xml) {
    xml = xml.replace(/<!--[\s\S]*?-->/g, '');
    const get = name => {
        const values = [...xml.matchAll(new RegExp(`<${name}>([^<]+)</${name}>`, 'g'))];
        const groups = [...xml.matchAll(/<PropertyGroup([^>]*)>([\s\S]*?)<\/PropertyGroup>/g)].filter(group => group[2].includes(`<${name}`));
        if (values.length !== 1 || values[0][1].includes('$(') || groups.length !== 1 || /Condition\s*=/.test(groups[0][1])) throw new Error(`Ambiguous ${name} in publication properties.`);
        return values[0][1].replaceAll('&amp;', '&').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&quot;', '"').replaceAll('&apos;', "'");
    };
    const version = get('Version'), label = get('SoundScriptVersionLabel'), codename = get('SoundScriptCodename');
    if (!semver(version) || label !== `V${version.split('.')[0]}` || !codename.trim()) throw new Error('Invalid SemVer or inconsistent release identity.');
    return { version, label, codename };
}

export function validatePublication(run, jobs, repository) {
    if (run.repository?.full_name !== repository || run.head_repository?.full_name !== repository ||
        run.path !== '.github/workflows/publish-nuget.yml' || run.head_branch !== 'main' ||
        run.event !== 'workflow_dispatch' || run.conclusion !== 'success' || !/^[a-f0-9]{40}$/.test(run.head_sha))
        throw new Error('Publication must be a successful publish-nuget.yml dispatch from this repository main.');
    const steps = jobs.flatMap(job => job.steps ?? []).filter(step => step.name === 'Publish to nuget.org');
    if (steps.length !== 1 || steps[0].conclusion !== 'success') throw new Error('Publish step did not run successfully (publish=false is not publication).');
    return run.head_sha;
}

export function matchIdentity(published, current) {
    if (JSON.stringify(published) !== JSON.stringify(current)) throw new Error(`Publication ${published.version} does not match current main ${current.version} identity; main may have advanced. Do not promote a different release.`);
}

export async function waitForNuget(version, {
    fetchIndex = async remaining => {
        const response = await fetch('https://api.nuget.org/v3-flatcontainer/soundscript/index.json', { signal: AbortSignal.timeout(Math.max(1, Math.min(15000, remaining))), cache: 'no-store' });
        if (!response.ok) throw new Error(`NuGet HTTP ${response.status}`);
        return response.json();
    }, sleep = ms => new Promise(resolve => setTimeout(resolve, ms)), now = Date.now, timeout = 3600000,
    report = console.log,
} = {}) {
    if (!semver(version) || version.includes('+')) throw new Error('Invalid or unsupported NuGet version; build metadata must not be silently normalized.');
    const deadline = now() + timeout;
    let detail = 'exact version not listed';
    do {
        try {
            const result = await fetchIndex(Math.max(1, deadline - now()));
            if (Array.isArray(result.versions) && result.versions.includes(version.toLowerCase())) return;
            detail = 'exact version not listed';
        } catch (error) { detail = error.message; }
        const remaining = deadline - now();
        if (remaining <= 0) break;
        report(`Waiting for SoundScript ${version} on NuGet: ${detail}; ${Math.ceil(remaining / 60000)} minute(s) remaining.`);
        await sleep(Math.min(30000, remaining));
    } while (now() < deadline);
    throw new Error(`SoundScript ${version} public availability not verified: ${detail}. Publication may have succeeded; rerun promotion after NuGet indexing.`);
}

export function promote(state, notes, published, current) {
    matchIdentity(published, current);
    validateState(state, current.version);
    if (compareVersions(state.publicVersion, published.version) > 0) throw new Error('Cannot downgrade public release.');
    const headings = notes.match(/^## [^\r\n]+/gm) ?? [];
    const matching = headings.filter(heading => heading.split(/\s+/)[1] === published.version);
    const expected = `## ${published.version} — ${published.codename}`;
    if (matching.length !== 1 || ![expected, `${expected} (unreleased)`, `${expected} (unpublished candidate)`].includes(matching[0]))
        throw new Error(`Release heading missing, duplicated, or structurally inconsistent. Expected "${expected}" with an optional (unreleased) or (unpublished candidate) suffix; found ${JSON.stringify(matching)}.`);
    const next = structuredClone(state);
    if (state.publicVersion !== published.version) next.cli = { nugetPublished: false, githubReleasePublished: false };
    next.publicVersion = published.version;
    next.library.nugetPublished = true;
    return { state: next, notes: notes.replace(matching[0], expected) };
}

export const allowedFiles = new Set([
    'docs/release-state.json', 'RELEASE_NOTES.md', 'README.md', 'docs/404.html', 'docs/cli.md',
    'docs/contact/index.html', 'docs/doc.html', 'docs/documentation.md', 'docs/index.html',
    'docs/industrial/index.html', 'docs/nuget-end-to-end.md', 'docs/nuget.md', 'docs/quick-start.md',
    'docs/releasing.md', 'packaging/README.md', 'src/SoundScript.Playground/wwwroot/index.html',
]);
export function guardChanges(files) {
    for (const file of files) if (!allowedFiles.has(file)) throw new Error(`Unexpected promotion change: ${file}`);
    return files;
}
export function existingPromotion(prs, branch) {
    const matches = prs.filter(pr => pr.headRefName === branch && pr.baseRefName === 'main' && pr.state === 'OPEN');
    if (matches.length > 1) throw new Error('Multiple promotion PRs require manual resolution.');
    return matches[0] ?? null;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        if (process.argv[2] !== 'availability') throw new Error('Usage: release-promotion.mjs availability <version>');
        await waitForNuget(process.argv[3]);
        console.log(`PASS: SoundScript ${process.argv[3]} is listed on public NuGet.org.`);
    } catch (error) { console.error(error.message); process.exitCode = 1; }
}
