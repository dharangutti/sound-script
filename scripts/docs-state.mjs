import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const marked = require('../docs/assets/marked.min.js');
export const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const slash = p => p.replaceAll('\\', '/');
const read = (root, p) => {
    try { return fs.readFileSync(path.join(root, p), 'utf8'); }
    catch (error) { fail(p, 'SOURCE', 'READ', error.code, 'readable source file'); }
};
function json(root, p) {
    try { return JSON.parse(read(root, p).replace(/^\ufeff/, '')); }
    catch (error) { fail(p, 'SCHEMA', 'JSON', error.message, 'valid JSON'); }
}
export function fail(file, category, id, actual, expected, action = 'Correct the source and run ./scripts/update-docs.ps1.') {
    throw new Error(`${file} [${category}/${id}] Actual: ${actual}. Expected: ${expected}. Action: ${action}`);
}
const check = (ok, ...args) => { if (!ok) fail(...args); };
const object = x => x && typeof x === 'object' && !Array.isArray(x);
function keys(value, expected, file, category) {
    check(object(value) && Object.keys(value).sort().join() === [...expected].sort().join(), file, category, 'FIELDS', JSON.stringify(value), expected.join(', '));
}
export function semver(value) {
    if (typeof value !== 'string') return null;
    const m = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([\da-zA-Z-]+(?:\.[\da-zA-Z-]+)*))?(?:\+([\da-zA-Z-]+(?:\.[\da-zA-Z-]+)*))?$/.exec(value);
    if (!m || m[4]?.split('.').some(x => /^\d+$/.test(x) && x.length > 1 && x[0] === '0')) return null;
    return { core: m.slice(1, 4).map(BigInt), pre: m[4]?.split('.') };
}
export function compareVersions(a, b) {
    const x = semver(a), y = semver(b);
    for (let i = 0; i < 3; i++) if (x.core[i] !== y.core[i]) return x.core[i] > y.core[i] ? 1 : -1;
    if (!x.pre || !y.pre) return x.pre ? -1 : y.pre ? 1 : 0;
    for (let i = 0; i < Math.max(x.pre.length, y.pre.length); i++) {
        const a = x.pre[i], b = y.pre[i];
        if (a === b) continue;
        if (a === undefined || b === undefined) return a === undefined ? -1 : 1;
        const an = /^\d+$/.test(a), bn = /^\d+$/.test(b);
        if (an && bn) return BigInt(a) > BigInt(b) ? 1 : -1;
        if (an !== bn) return an ? -1 : 1;
        return a > b ? 1 : -1;
    }
    return 0;
}
export function validateState(s, development) {
    const f = 'docs/release-state.json';
    keys(s, ['schemaVersion', 'publicVersion', 'library', 'cli'], f, 'STATE');
    check(s.schemaVersion === 1, f, 'STATE', 'SCHEMA', s.schemaVersion, 1);
    keys(s.library, ['nugetPublished'], f, 'STATE');
    keys(s.cli, ['nugetPublished', 'githubReleasePublished'], f, 'STATE');
    for (const [name, obj] of [['library', s.library], ['cli', s.cli]]) for (const [key, value] of Object.entries(obj))
        check(typeof value === 'boolean', f, 'STATE', `${name}.${key}`, value, 'boolean');
    check(semver(s.publicVersion) && semver(development), f, 'STATE', 'VERSION', `${s.publicVersion} / ${development}`, 'semantic versions');
    check(compareVersions(s.publicVersion, development) <= 0, f, 'STATE', 'ORDER', s.publicVersion, `<= development ${development}`);
    return s;
}
// Deliberately supports only unambiguous literal properties. A future move to
// conditions/imports must supply an evaluated authority rather than silently guessing.
function property(root, file, name) {
    const source = read(root, file).replace(/<!--[\s\S]*?-->/g, '');
    const groups = [...source.matchAll(/<PropertyGroup([^>]*)>([\s\S]*?)<\/PropertyGroup>/g)];
    const definitions = groups.flatMap(g => [...g[2].matchAll(new RegExp(`<${name}(?:\\s[^>]*)?>([^<]+)</${name}>`, 'g'))].map(m => ({ group: g[1], markup: m[0] })));
    check(definitions.length === 1 && !definitions[0].group.trim() && definitions[0].markup.startsWith(`<${name}>`), file, 'SOURCE', name, 'conditional or ambiguous definition', 'one unconditional literal property');
    const xml = groups.map(m => m[2]).join('\n');
    const hits = [...xml.matchAll(new RegExp(`<${name}>([^<]+)</${name}>`, 'g'))];
    check(hits.length === 1 && !hits[0][1].includes('$('), file, 'SOURCE', name, `${hits.length} literal definitions`, 'one unconditional literal property');
    return hits[0][1].replaceAll('&amp;', '&').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&quot;', '"').replaceAll('&apos;', "'");
}
export function facts(root) {
    const props = 'Directory.Build.props', lib = 'src/SoundScript/SoundScript.csproj', cli = 'src/SoundScript.Cli/SoundScript.Cli.csproj';
    const f = { development: property(root, props, 'Version'), label: property(root, props, 'SoundScriptVersionLabel'), codename: property(root, props, 'SoundScriptCodename'),
        packageId: property(root, lib, 'PackageId'), framework: property(root, lib, 'TargetFramework'), repository: property(root, lib, 'RepositoryUrl').replace(/\.git$/, ''),
        site: property(root, lib, 'PackageProjectUrl'), cliId: property(root, cli, 'PackageId'), command: property(root, cli, 'ToolCommandName') };
    check(f.label === `V${f.development.split('.')[0]}`, props, 'SOURCE', 'LABEL', f.label, `V${f.development.split('.')[0]}`);
    check(/^net\d+\.\d+$/.test(f.framework), lib, 'SOURCE', 'FRAMEWORK', f.framework, 'literal net major.minor');
    check(property(root, cli, 'TargetFramework') === f.framework, cli, 'SOURCE', 'FRAMEWORK', 'CLI target', f.framework);
    f.state = validateState(json(root, 'docs/release-state.json'), f.development);
    return f;
}
export function inScope(p) {
    return /^[^/]+\.md$/.test(p) || /^docs\/.*\.(md|html|json|xml|js|css)$/.test(p) || /^packaging\/.*\.md$/.test(p) ||
        /^(samples|tutorials)\/.*README\.md$/.test(p) || /^src\/.*\.csproj$/.test(p) ||
        /^src\/SoundScript.Playground\/wwwroot\/index\.html$/.test(p) || /^scripts\/[^/]+\.(ps1|sh|cjs|mjs)$/.test(p) || /^\.github\/workflows\/.*\.ya?ml$/.test(p);
}
export function inventory(root) {
    const files = [];
    function walk(dir, relative = '') {
        for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
            const p = relative + e.name;
            if (e.isSymbolicLink()) continue;
            if (e.isDirectory()) {
                if (['.git', 'node_modules', 'bin', 'obj', 'artifacts', 'TestResults', '_framework', '.vs'].includes(e.name) || p === 'experiments/SoundScript.Labs') continue;
                if (!relative && !['docs', 'packaging', 'samples', 'tutorials', 'src', 'scripts', '.github'].includes(e.name)) continue;
                walk(path.join(dir, e.name), p + '/');
            } else if (inScope(p)) files.push(p);
        }
    }
    walk(root);
    return files.sort();
}
const safePath = p => typeof p === 'string' && !p.includes('\\') && !p.startsWith('/') && !p.includes(':') && p.split('/').every(s => s && s !== '..' && s !== '.');
const matches = (p, pattern) => new RegExp('^' + pattern.split('*').map(s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('.*') + '$').test(p);
export function validateManifest(m, files) {
    const f = 'docs/docs-manifest.json';
    keys(m, ['schemaVersion', 'livingDocuments', 'historicalDocuments', 'historicalPatterns', 'generatedOnly', 'excluded', 'generatedBlocks', 'classificationOverrides'], f, 'MANIFEST');
    check(m.schemaVersion === 1, f, 'MANIFEST', 'SCHEMA', m.schemaVersion, 1);
    for (const k of ['livingDocuments', 'historicalDocuments', 'generatedOnly', 'historicalPatterns'])
        check(Array.isArray(m[k]) && m[k].every(safePath) && new Set(m[k]).size === m[k].length, f, 'MANIFEST', k, 'invalid list/duplicate/path', 'unique repository paths');
    for (const k of ['excluded', 'generatedBlocks', 'classificationOverrides']) check(object(m[k]), f, 'MANIFEST', k, 'invalid object', 'object');
    const classes = new Map();
    for (const [key, kind] of [['livingDocuments', 'living'], ['historicalDocuments', 'historical'], ['generatedOnly', 'generated-only']]) for (const p of m[key]) {
        check(files.includes(p), p, 'MANIFEST', 'MISSING', 'declared file absent or out of scope', 'existing in-scope file');
        check(!classes.has(p), p, 'CLASSIFICATION', 'CONFLICT', `${classes.get(p)} and ${kind}`, 'one classification');
        classes.set(p, kind);
    }
    for (const [p, reason] of Object.entries(m.excluded)) {
        check(safePath(p) && files.includes(p) && typeof reason === 'string' && reason.trim().length > 10, p, 'MANIFEST', 'EXCLUSION', reason, 'existing exact path with meaningful reason');
        check(!classes.has(p), p, 'CLASSIFICATION', 'CONFLICT', 'excluded and classified', 'one classification');
        classes.set(p, 'excluded');
    }
    for (const pattern of m.historicalPatterns) check(files.some(p => matches(p, pattern)), f, 'MANIFEST', 'PATTERN', pattern, 'pattern matching a scoped file');
    for (const p of files) {
        const historical = m.historicalPatterns.some(pattern => matches(p, pattern));
        if (historical && classes.has(p) && classes.get(p) !== 'historical') {
            const override = m.classificationOverrides[p];
            check(object(override) && override.classification === classes.get(p) && typeof override.reason === 'string' && override.reason.trim().length > 10,
                p, 'CLASSIFICATION', 'PATTERN_CONFLICT', classes.get(p), 'explicit justified classificationOverrides entry');
        } else if (historical) classes.set(p, 'historical');
        check(classes.has(p), p, 'CLASSIFICATION', 'UNCLASSIFIED', 'no classification', 'manifest declaration');
    }
    for (const p of Object.keys(m.classificationOverrides)) check(files.includes(p) && m.historicalPatterns.some(pattern => matches(p, pattern)) && classes.get(p) !== 'historical', p, 'MANIFEST', 'OVERRIDE', 'unused override', 'only a justified pattern conflict');
    for (const [p, blocks] of Object.entries(m.generatedBlocks)) check(classes.get(p) === 'living' && Array.isArray(blocks) && blocks.length > 0 && blocks.every(b => /^[A-Z][A-Z_]+$/.test(b)) && new Set(blocks).size === blocks.length,
        p, 'MANIFEST', 'BLOCK_MAP', JSON.stringify(blocks), 'unique allowed block IDs in living files');
    return classes;
}
export function markerBlocks(text, allowed, file, historical = false) {
    const tokens = [...text.matchAll(/<!--\s*GENERATED:([\s\S]*?)-->/g)];
    check(tokens.length === (text.match(/<!--\s*GENERATED:/g) || []).length, file, 'MARKER', 'FORMAT', 'unterminated marker', 'closed HTML comment');
    const blocks = [], seen = new Set(); let open;
    for (const t of tokens) {
        const m = /^([A-Z][A-Z_]+)_(START|END)\s*$/.exec(t[1]);
        check(m && !historical, file, 'MARKER', 'FORMAT', t[0], 'valid marker in living document');
        const [, id, edge] = m;
        check(allowed.includes(id), file, 'MARKER', id, 'undeclared marker', 'ID declared for file');
        if (edge === 'START') {
            check(!open && !seen.has(id), file, 'MARKER', id, 'nested or duplicate START', 'one non-nested ordered pair');
            open = { id, start: t.index + t[0].length };
        } else {
            check(open?.id === id, file, 'MARKER', id, 'END without matching START', 'matching ordered pair');
            blocks.push({ ...open, end: t.index }); seen.add(id); open = null;
        }
    }
    check(!open, file, 'MARKER', open?.id || 'PAIR', 'missing END', 'complete pair');
    for (const id of allowed) check(seen.has(id), file, 'MARKER', id, 'missing block', 'one START/END pair');
    return blocks;
}
const fence = (language, code) => '```' + language + '\n' + code + '\n```';
export function render(id, f, original = '') {
    const s = f.state, v = s.publicVersion;
    switch (id) {
        case 'LIBRARY_INSTALL': return s.library.nugetPublished ? `Install the published library:\n\n${fence('bash', `dotnet add package ${f.packageId} --version ${v}`)}\n\n[${f.packageId} ${v} on NuGet](https://www.nuget.org/packages/${f.packageId}/${v}).` : `${f.packageId} ${v} is not published on nuget.org. Use a source checkout for development.`;
        case 'CLI_DISTRIBUTION': return (s.cli.nugetPublished ? fence('bash', `dotnet tool install --global ${f.cliId} --version ${v}`) : `\`${f.cliId}\` ${v} is not published on nuget.org.`) +
            (s.cli.githubReleasePublished ? `\n\nDownload a platform archive from [CLI ${v}](${f.repository}/releases/tag/v${v}), verify its SHA-256 checksum, extract it, and run \`${f.command}\` from that directory.` : '\n\nNo CLI GitHub Release is published for this public version. Build from a source checkout.');
        case 'CURRENT_PUBLIC_RELEASE': return `Current public version: **${v}**. Publication channels are recorded in \`docs/release-state.json\`.`;
        case 'CURRENT_DEVELOPMENT_VERSION': return `Development: **${f.development} / ${f.label} — ${f.codename}**. Development identity does not imply publication.`;
        case 'DOTNET_REQUIREMENT': return `Requires .NET ${f.framework.slice(3)} (\`${f.framework}\`). Use the SDK selected by \`global.json\` for repository development.`;
        case 'LOCAL_LIBRARY_INSTALL': return fence('powershell', `dotnet pack src/SoundScript/SoundScript.csproj -c Release --output artifacts/nuget\ndotnet new console -n PackageConsumer -f ${f.framework}\ndotnet add PackageConsumer/PackageConsumer.csproj package ${f.packageId} --version ${f.development} --source artifacts/nuget\ndotnet run --project PackageConsumer/PackageConsumer.csproj`);
        case 'PUBLIC_BADGE': return `Version: V${v.split('.')[0]} (${v})`;
        case 'SITE_METADATA': {
            // HTML markers surround the script, never live inside JSON/script code.
            const match = /^(\s*<script\b[^>]*>)([\s\S]*?)(<\/script>\s*)$/.exec(original.trim());
            check(match, 'site metadata', 'RENDER', id, 'invalid script', 'one JSON-LD script');
            const data = JSON.parse(match[2]);
            const visit = value => { if (!object(value) && !Array.isArray(value)) return;
                if (value['@type'] === 'SoftwareApplication') { value.softwareVersion = v; if ('softwareHelp' in value) value.softwareHelp = `${f.site}doc.html?p=documentation.md`; }
                Object.values(value).forEach(visit);
            };
            visit(data);
            return match[1] + '\n' + JSON.stringify(data, null, 4).replaceAll('<', '\\u003c') + '\n' + match[3];
        }
        default: fail('docs/docs-manifest.json', 'RENDER', id, 'unknown block', 'supported renderer');
    }
}
export function generate(text, blocks, f) {
    let result = text;
    for (const b of [...blocks].reverse()) {
        const original = text.slice(b.start, b.end);
        // A few existing HTML files have mixed endings; preserve the block's
        // own convention rather than normalizing unrelated source bytes.
        const nl = original.includes('\r\n') ? '\r\n' : original.includes('\n') ? '\n' : text.includes('\r\n') ? '\r\n' : '\n';
        result = result.slice(0, b.start) + nl + render(b.id, f, original).replace(/\r?\n/g, nl) + nl + result.slice(b.end);
    }
    return result;
}
export function validateCurrent(text, file, f) {
    // Historical/comparison sections are explicitly delimited and reviewed, never
    // inferred from an old number. This avoids treating every version as current.
    const historyMarkers = [...text.matchAll(/<!-- HISTORICAL_CONTEXT_(START|END) -->/g)].map(m => m[1]);
    check(historyMarkers.every((edge, i) => edge === (i % 2 ? 'END' : 'START')) && historyMarkers.length % 2 === 0, file, 'CURRENT', 'HISTORICAL_CONTEXT', 'invalid historical delimiter sequence', 'non-nested START/END pairs');
    const current = text.replace(/<!-- HISTORICAL_CONTEXT_START -->[\s\S]*?<!-- HISTORICAL_CONTEXT_END -->/g, '');
    const logical = current.replace(/(?:\\|`)\r?\n\s*/g, ' ');
    for (const m of logical.matchAll(/dotnet\s+(?:add\s+[^\r\n]*?package|tool\s+install)\s+([^\r\n<]+)/g)) {
        const command = m[0], local = /--(?:add-)?source\s+(?!https?:\/\/api\.nuget\.org)/.test(command);
        const id = command.includes(f.cliId) ? f.cliId : command.includes(f.packageId) ? f.packageId : null;
        if (!id) continue;
        const version = /--version\s+([\w.+-]+)/.exec(command)?.[1];
        if (local) {
            if (version && semver(version)) check(version === f.development, file, 'CURRENT', 'LOCAL_INSTALL', version, f.development);
            continue;
        }
        const published = id === f.cliId ? f.state.cli.nugetPublished : f.state.library.nugetPublished;
        check(published, file, 'DISTRIBUTION', 'NUGET', command, `${id} channel enabled for public version`);
        check(version === f.state.publicVersion, file, 'CURRENT', 'INSTALL', version || 'unpinned version', f.state.publicVersion);
    }
    for (const m of current.matchAll(/(?:https:\/\/www\.nuget\.org\/packages\/[^/\s)"<>]+\/|\/releases\/tag\/v)([\d][\w.+-]*)/g)) {
        check(m[1] === f.state.publicVersion, file, 'CURRENT', 'PUBLIC_LINK', m[1], f.state.publicVersion);
        check(m[0].includes('/releases/') ? f.state.cli.githubReleasePublished : f.state.library.nugetPublished, file, 'DISTRIBUTION', 'PUBLIC_LINK', m[0], 'enabled publication channel');
    }
    for (const m of current.matchAll(/(?:softwareVersion["']?\s*:\s*["']|current (?:public )?(?:release|version)(?: is|:)?\s*[`*"']*)(\d+\.\d+\.\d+)/gi))
        check(m[1] === f.state.publicVersion, file, 'CURRENT', 'IDENTITY', m[1], f.state.publicVersion);
    const major = f.state.publicVersion.split('.')[0];
    check(!new RegExp(`(?:V${major}|${f.state.publicVersion.replaceAll('.', '\\.')}|current|public)(?:\\s+\\w+){0,3}\\s+candidate`, 'i').test(current), file, 'CURRENT', 'CANDIDATE', 'public artifact called candidate', 'published current artifact or explicit historical context');
    for (const m of current.matchAll(/(?:install (?:the )?(?:published |current )?|current (?:release|package|version)\s*(?:is|:)\s*)V(\d+)\b/gi))
        check(m[1] === major, file, 'CURRENT', 'LABEL', m[0], `public V${major}`);
    for (const m of current.matchAll(/(?:SoundScript\.Cli|(?:The )?CLI)`?\s+(?:\d+\.\d+\.\d+\s+)?is (not )?published on nuget\.org/gi))
        check(Boolean(m[1]) !== f.state.cli.nugetPublished, file, 'DISTRIBUTION', 'CLI_CLAIM', m[0], `CLI NuGet publication ${f.state.cli.nugetPublished}`);
}
export function slug(text) { return text.toLowerCase().replace(/<[^>]*>/g, '').replace(/[^\p{L}\p{N}_\s-]/gu, '').trim().replace(/\s/g, '-'); }
function markdownLinks(text) {
    const links = [];
    marked.walkTokens(marked.lexer(text), token => { if (['link', 'image'].includes(token.type)) links.push(token.href); });
    return links;
}
export function validateLinks(text, file, root) {
    for (const href of markdownLinks(text)) {
        if (/^(?:[a-z]+:|\/\/)/i.test(href)) continue;
        const [part, fragment] = href.split('#');
        if (part.includes('?')) continue;
        const target = part ? (part.startsWith('/') ? path.join(root, 'docs', part.slice(1)) : path.resolve(root, path.dirname(file), decodeURIComponent(part))) : path.join(root, file);
        check(target.startsWith(root + path.sep) && fs.existsSync(target), file, 'LINK', 'TARGET', href, 'existing repository target');
        if (fragment && target.endsWith('.md')) {
            const headings = new Set(); const counts = new Map();
            marked.walkTokens(marked.lexer(fs.readFileSync(target, 'utf8')), token => {
                if (token.type !== 'heading') return;
                const s = slug(token.text), n = counts.get(s) || 0; counts.set(s, n + 1); headings.add(s + (n ? `-${n}` : ''));
            });
            check(headings.has(decodeURIComponent(fragment)), file, 'LINK', 'FRAGMENT', href, 'existing heading fragment');
        }
    }
}
export function validateCli(root, text) {
    const source = read(root, 'src/SoundScript.Cli/CliArguments.cs');
    const region = /CommandDefinition\[\] Commands\s*=\s*\[([\s\S]*?)\];/.exec(source)?.[1];
    check(region, 'src/SoundScript.Cli/CliArguments.cs', 'CLI', 'SOURCE', 'registration shape', 'recognizable Commands array');
    const commands = [...region.matchAll(/new\("([^"]+)"/g)].map(m => m[1]);
    const documented = [...text.matchAll(/^\| `([^`]+)` \| public \|/gm)].map(m => m[1]);
    check(commands.sort().join() === documented.sort().join(), 'docs/cli.md', 'CLI', 'COVERAGE', documented.join(', '), commands.join(', '));
    const exitSource = /ExitCode\(Exception ex\) => ex switch\s*\{([\s\S]*?)\};/.exec(read(root, 'src/SoundScript.Cli/Diagnostics.cs'))?.[1];
    check(exitSource, 'src/SoundScript.Cli/Diagnostics.cs', 'EXIT', 'SOURCE', 'unrecognized switch', 'exception-to-value switch');
    const mappings = [...exitSource.matchAll(/([\w ]+)\s*=>\s*(\d+)/g)].map(m => [m[1].trim(), m[2]]);
    for (const [exception, code] of mappings) check(text.includes(`| \`${exception}\` | \`${code}\` |`), 'docs/cli.md', 'EXIT', exception, 'mapping absent/different', code);
    check(/return 0;/.test(read(root, 'src/SoundScript.Cli/Program.cs')) && /\| `success` \| `0` \|/.test(text), 'docs/cli.md', 'EXIT', 'SUCCESS', 'success mapping', '0');
}
export function validateHub(root, texts) {
    const viewer = texts.get('docs/doc.html');
    check(viewer.includes("var DEFAULT_PAGE = 'documentation.md';"), 'docs/doc.html', 'HUB', 'DEFAULT', 'viewer default', 'documentation.md');
    check(texts.get('docs/SoundScript.md').includes('(documentation.md)'), 'docs/SoundScript.md', 'HUB', 'COMPATIBILITY', 'legacy gateway', 'link to documentation.md');
    for (const [p, text] of texts) if (/\.(html|xml)$/.test(p)) check(!/doc\.html\?p=SoundScript\.md|https:\/\/soundscript\.net\/SoundScript\.md/.test(text), p, 'HUB', 'NAVIGATION', 'legacy entry URL', 'canonical documentation.md URL');
    for (const [p, text] of texts) if (/\.(html|xml)$/.test(p)) for (const m of text.matchAll(/doc\.html\?p=([a-zA-Z0-9._%/-]+\.md)/g)) {
        const target = decodeURIComponent(m[1]);
        check(safePath(target) && fs.existsSync(path.join(root, 'docs', target)), p, 'LINK', 'DOCS_NAV', target, 'existing docs Markdown page');
    }
}
export function run(root = repo, checkOnly = false) {
    const f = facts(root), files = inventory(root), m = json(root, 'docs/docs-manifest.json');
    const classes = validateManifest(m, files), outputs = new Map(), pending = [];
    for (const p of files) {
        const kind = classes.get(p); if (kind === 'excluded') continue;
        const before = read(root, p);
        // Script literals describe markers and are not document markers themselves.
        const blocks = /\.(md|html)$/.test(p) ? markerBlocks(before, m.generatedBlocks[p] || [], p, kind === 'historical') : [];
        const after = generate(before, blocks, f);
        if (before !== after) pending.push({ p, before, after });
        if (kind === 'living') {
            outputs.set(p, after);
            if (/\.(md|html)$/.test(p)) validateCurrent(after, p, f);
            if (p.endsWith('.md')) validateLinks(after, p, root);
        }
    }
    validateCli(root, outputs.get('docs/cli.md'));
    validateHub(root, outputs);
    check(read(root, 'RELEASE_NOTES.md').includes(`## ${f.development} —`), 'RELEASE_NOTES.md', 'NOTES', 'DEVELOPMENT', 'missing heading', f.development);
    if (checkOnly && pending.length) fail(pending[0].p, 'DRIFT', (m.generatedBlocks[pending[0].p] || []).join(','), 'generated body differs', 'canonical rendered content', 'Run ./scripts/update-docs.ps1.');
    // All validation completes before any writes. UTF-8 BOM and existing newlines
    // survive because replacement operates only on marker body slices.
    if (!checkOnly) for (const { p, after } of pending) fs.writeFileSync(path.join(root, p), after, 'utf8');
    return { files: files.length, changed: pending.map(x => x.p), publicVersion: f.state.publicVersion, developmentVersion: f.development };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try { console.log(JSON.stringify(run(repo, process.argv.includes('--check')), null, 2)); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
}
