import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { facts, validateState, semver, compareVersions, validateManifest, markerBlocks, render, generate, validateCurrent, validateLinks, validateCli, validateHub, inventory, run, repo } from './docs-state.mjs';
const state = () => ({ schemaVersion: 1, publicVersion: '14.0.0', library: { nugetPublished: true }, cli: { nugetPublished: false, githubReleasePublished: true } });
const f = facts(repo);
const manifest = () => ({ schemaVersion: 1, livingDocuments: ['a.md'], historicalDocuments: [], historicalPatterns: [], generatedOnly: [], excluded: {}, generatedBlocks: {}, classificationOverrides: {} });
const start = '<!-- GENERATED:LIBRARY_INSTALL_START -->', end = '<!-- GENERATED:LIBRARY_INSTALL_END -->';
const block = start + '\nold\n' + end;
test('valid release state and strict SemVer precedence', () => {
    assert.deepEqual(validateState(state(), '15.0.0'), state());
    for (const bad of ['14.0', 'v14.0.0', '01.0.0', '14.0.0-01', '14.0.0-', '14.0.0+']) assert.equal(semver(bad), null);
    for (const [a,b] of [['14.0.0-rc.2','14.0.0-rc.10'],['14.0.0-rc.1','14.0.0'],['14.0.0-1','14.0.0-alpha'],['14.0.0','15.0.0']]) assert.equal(compareVersions(a,b), -1);
    assert.equal(compareVersions('14.0.0+one','14.0.0+two'), 0);
});
for (const [name, mutate] of [
    ['unsupported schema', s => s.schemaVersion = 2], ['invalid version', s => s.publicVersion = 'bad'],
    ['newer public version', s => s.publicVersion = '16.0.0'], ['wrong flag type', s => s.cli.nugetPublished = 'false'],
    ['missing field', s => delete s.library], ['extra duplicated fact', s => s.packageId = 'SoundScript']
]) test(`state rejects ${name}`, () => { const s = state(); mutate(s); assert.throws(() => validateState(s, '15.0.0'), /STATE/); });
test('manifest completeness, missing files, conflicts, exclusions and schema', () => {
    assert.equal(validateManifest(manifest(), ['a.md']).get('a.md'), 'living');
    assert.throws(() => validateManifest(manifest(), ['a.md','new.md']), /UNCLASSIFIED/);
    assert.throws(() => validateManifest(manifest(), []), /MISSING/);
    const m = manifest(); m.historicalDocuments.push('a.md'); assert.throws(() => validateManifest(m, ['a.md']), /CONFLICT/);
    const e = manifest(); e.excluded['b.md'] = ''; assert.throws(() => validateManifest(e, ['a.md','b.md']), /EXCLUSION/);
    e.excluded['b.md'] = 'Generated build output checked separately'; assert.equal(validateManifest(e, ['a.md','b.md']).get('b.md'), 'excluded');
    const v = manifest(); v.schemaVersion = 99; assert.throws(() => validateManifest(v, ['a.md']), /SCHEMA/);
});
test('explicit pattern override requires justification and exact classification', () => {
    const m = manifest(); m.historicalPatterns = ['*.md']; assert.throws(() => validateManifest(m, ['a.md']), /PATTERN_CONFLICT/);
    m.classificationOverrides['a.md'] = { classification: 'living', reason: 'Intentional current guide despite version-like name' };
    assert.equal(validateManifest(m, ['a.md']).get('a.md'), 'living');
    m.historicalDocuments.push('a.md'); assert.throws(() => validateManifest(m, ['a.md']), /CONFLICT/);
});
for (const [name, source] of [ ['missing START', end], ['missing END', start], ['duplicate START', start + block],
    ['duplicate END', block + end], ['mismatch', start + '<!-- GENERATED:CLI_DISTRIBUTION_END -->'],
    ['nested', start + '<!-- GENERATED:CLI_DISTRIBUTION_START -->' + end], ['reversed', end + start],
    ['undeclared', '<!-- GENERATED:OTHER_START -->x<!-- GENERATED:OTHER_END -->'], ['malformed', '<!-- GENERATED:bad_START -->']
]) test(`markers reject ${name}`, () => assert.throws(() => markerBlocks(source, ['LIBRARY_INSTALL','CLI_DISTRIBUTION'], 'a.md'), /MARKER/));
test('historical documents prohibit current generated blocks', () => assert.throws(() => markerBlocks(block, ['LIBRARY_INSTALL'], 'old.md', true), /MARKER/));
test('renderer preserves BOM, CRLF and surrounding bytes; idempotent', () => {
    const source = '\ufeffBefore\r\n' + block.replaceAll('\n','\r\n') + '\r\nAfter';
    const out = generate(source, markerBlocks(source, ['LIBRARY_INSTALL'], 'a.md'), f);
    assert.ok(out.startsWith('\ufeffBefore\r\n' + start)); assert.ok(out.endsWith(end + '\r\nAfter'));
    assert.ok(!/(?<!\r)\n/.test(out));
    assert.equal(generate(out, markerBlocks(out, ['LIBRARY_INSTALL'], 'a.md'), f), out);
    assert.ok(out.includes(`--version ${f.state.publicVersion}`));
});
test('all publication channels control generation, including future CLI NuGet', () => {
    const next = structuredClone(f); next.state.cli.nugetPublished = true;
    assert.ok(render('CLI_DISTRIBUTION', next).includes(`dotnet tool install --global SoundScript.Cli --version ${next.state.publicVersion}`));
    next.state.cli.nugetPublished = false; next.state.cli.githubReleasePublished = false; next.state.library.nugetPublished = false;
    assert.doesNotMatch(render('CLI_DISTRIBUTION', next), /dotnet tool install|releases\/tag/);
    assert.doesNotMatch(render('LIBRARY_INSTALL', next), /dotnet add|nuget.org\/packages/);
});
test('current validation rejects stale installs, claims, links and distribution contradictions', () => {
    for (const source of ['dotnet add package SoundScript --version 13.0.0', 'dotnet tool install --global SoundScript.Cli',
        'Current release is 13.0.0', `The V${f.state.publicVersion.split('.')[0]} candidate package is ready`, 'https://github.com/dharangutti/sound-script/releases/tag/v13.0.0',
        `dotnet add package SoundScript --version ${f.state.publicVersion} --source https://api.nuget.org/v3/index.json`]) {
        const next = structuredClone(f); if (source.includes('api.nuget')) next.state.library.nugetPublished = false;
        assert.throws(() => validateCurrent(source, 'a.md', next), /CURRENT|DISTRIBUTION/);
    }
    assert.doesNotThrow(() => validateCurrent('V14 is additive over 13.0.2. Algorithmic candidate notes are experimental.', 'a.md', f));
    assert.doesNotThrow(() => validateCurrent('<!-- HISTORICAL_CONTEXT_START -->Current release is 13.0.0; V14 candidate<!-- HISTORICAL_CONTEXT_END -->', 'a.md', f));
});
function fixture(t) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-docs-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    const write = (p, value) => { fs.mkdirSync(path.dirname(path.join(root,p)), {recursive:true}); fs.writeFileSync(path.join(root,p), value); };
    for (const p of ['Directory.Build.props','src/SoundScript/SoundScript.csproj','src/SoundScript.Cli/SoundScript.Cli.csproj','src/SoundScript.Cli/CliArguments.cs','src/SoundScript.Cli/Diagnostics.cs','src/SoundScript.Cli/Program.cs']) write(p,fs.readFileSync(path.join(repo,p)));
    write('docs/release-state.json',JSON.stringify(state()));
    const cli = fs.readFileSync(path.join(repo,'docs/cli.md'),'utf8');
    write('docs/cli.md',cli.slice(cli.indexOf('## Public command coverage')));
    write('docs/documentation.md','# Documentation\n[CLI](cli.md)\n');
    write('docs/SoundScript.md','# Compatibility\n[Hub](documentation.md)\n');
    write('docs/doc.html',"var DEFAULT_PAGE = 'documentation.md';");
    write('README.md','\ufeffBefore\r\n'+block.replaceAll('\n','\r\n')+'\r\nAfter');
    write('RELEASE_NOTES.md',`## ${f.development} — Development\nV14 candidate; historical 13.0.0 installs.`);
    write('docs/docs-manifest.json','{}');
    const m = manifest(); m.livingDocuments = inventory(root).filter(p => p !== 'RELEASE_NOTES.md'); m.historicalDocuments = ['RELEASE_NOTES.md']; m.generatedBlocks = {'README.md':['LIBRARY_INSTALL']};
    write('docs/docs-manifest.json',JSON.stringify(m));
    return {root,write};
}
const bytes = root => inventory(root).map(p => [p, fs.readFileSync(path.join(root,p)).toString('base64')]);
test('check detects drift without changing any bytes; update preserves history and is idempotent', t => {
    const {root} = fixture(t), before = bytes(root);
    assert.throws(() => run(root,true), /DRIFT/); assert.deepEqual(bytes(root),before);
    assert.equal(run(root).changed.length,1); const after=bytes(root);
    assert.deepEqual(run(root).changed,[]); assert.deepEqual(bytes(root),after);
    assert.deepEqual(run(root,true).changed,[]); assert.deepEqual(bytes(root),after);
    assert.deepEqual(after.find(([p])=>p==='RELEASE_NOTES.md'),before.find(([p])=>p==='RELEASE_NOTES.md'));
});
test('all validation precedes writes, even if a later file fails', t => {
    const {root,write}=fixture(t); write('docs/new.md','# Unclassified'); const before=bytes(root);
    assert.throws(()=>run(root),/UNCLASSIFIED/); assert.deepEqual(bytes(root),before);
});
test('malformed JSON has actionable file/category diagnostics without writes', t => {
    const {root,write}=fixture(t); write('docs/release-state.json','{ invalid'); const before=bytes(root);
    assert.throws(()=>run(root),/docs\/release-state.json \[SCHEMA\/JSON\].*Actual:.*Expected:.*Action:/);
    assert.deepEqual(bytes(root),before);
});
test('mixed newline files retain the generated block convention and unrelated bytes', () => {
    const source='CRLF heading\r\n'+block+'\nCRLF footer\r\n';
    const out=generate(source,markerBlocks(source,['LIBRARY_INSTALL'],'mixed.md'),f);
    assert.ok(out.startsWith('CRLF heading\r\n'+start+'\n'));assert.ok(out.endsWith(end+'\nCRLF footer\r\n'));
    assert.equal(generate(out,markerBlocks(out,['LIBRARY_INSTALL'],'mixed.md'),f),out);
});
test('local links, reference-style links and duplicate heading fragments are checked; fenced text ignored', t => {
    const {root,write}=fixture(t);write('docs/target.md','# Target\n## Same\n## Same\n');
    assert.doesNotThrow(()=>validateLinks('[ok](target.md#same-1)\n```text\n[not a link](missing.md)\n```','docs/a.md',root));
    assert.throws(()=>validateLinks('[bad][ref]\n\n[ref]: absent.md','docs/a.md',root),/TARGET/);
    assert.throws(()=>validateLinks('[bad](target.md#absent)','docs/a.md',root),/FRAGMENT/);
});
test('CLI registration, exit mapping and canonical navigation regressions fail', t => {
    const {root}=fixture(t), text=fs.readFileSync(path.join(root,'docs/cli.md'),'utf8');
    validateCli(root,text);
    assert.throws(()=>validateCli(root,text.replace('| `visual` | public |','')),/COVERAGE/);
    assert.throws(()=>validateCli(root,text.replace('| `CliUsageException` | `2` |','| `CliUsageException` | `9` |')),/EXIT/);
    const texts=new Map([['docs/doc.html',"var DEFAULT_PAGE = 'documentation.md';"],['docs/SoundScript.md','[Hub](documentation.md)']]);
    validateHub(root,texts); texts.set('docs/doc.html',"var DEFAULT_PAGE = 'SoundScript.md';"); assert.throws(()=>validateHub(root,texts),/HUB/);
});
test('inventory excludes labs, build output, vendored dependencies and catches new scope files', t => {
    const {root,write}=fixture(t);for(const p of ['artifacts/a.md','docs/bin/a.md','scripts/node_modules/a.mjs','experiments/SoundScript.Labs/a.md'])write(p,'ignored');
    write('docs/new.md','# New');const list=inventory(root);assert.ok(list.includes('docs/new.md'));assert.ok(!list.some(p=>/artifacts|node_modules|Labs|\/bin\//.test(p)));
});
test('repository documentation check passes without mutation', () => { const before=bytes(repo);run(repo,true);assert.deepEqual(bytes(repo),before); });
