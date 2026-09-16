// Read-only production evidence: sequential requests, no cache-busting query strings.
const fs = require('node:fs');
const crypto = require('node:crypto');
const { resourceFiles } = require('./verify-playground-integrity.cjs');
const sri = bytes => 'sha256-' + crypto.createHash('sha256').update(bytes).digest('base64');
async function get(url, cache) {
    const response = await fetch(url, { cache, signal: AbortSignal.timeout(30000) });
    const bytes = Buffer.from(await response.arrayBuffer());
    return { status: response.status, bytes, hash: sri(bytes), headers: Object.fromEntries(
        ['content-type', 'content-encoding', 'cache-control', 'age', 'etag', 'cf-cache-status', 'cf-ray', 'server', 'x-cache', 'last-modified'].map(key => [key, response.headers.get(key)])) };
}
(async () => {
    const base = process.argv[2] || 'https://soundscript.net/playground/';
    const report = { time: new Date().toISOString(), base, probes: [] };
    const boot = await get(new URL('_framework/dotnet.js', base));
    const match = boot.bytes.toString().match(/\/\*json-start\*\/([\s\S]*?)\/\*json-end\*\//);
    if (!match) throw new Error('Expected embedded .NET 10 boot configuration');
    const files = resourceFiles(JSON.parse(match[1]).resources);
    const target = files.find(file => file.name.startsWith('Microsoft.Extensions.DependencyInjection.Abstractions.'));
    for (const cache of ['default', 'no-cache', 'default', 'no-cache', 'default']) {
        const response = await get(new URL('_framework/' + target.name, base), cache);
        const { bytes, ...details } = response;
        report.probes.push({ name: target.name, cache, expected: target.hash, matches: response.hash === target.hash, length: bytes.length, ...details });
    }
    report.assets = [];
    for (const file of files.filter(file => file.hash)) {
        const response = await get(new URL('_framework/' + file.name, base));
        report.assets.push({ name: file.name, status: response.status, expected: file.hash, actual: response.hash, matches: file.hash === response.hash });
    }
    const again = await get(new URL('_framework/dotnet.js', base), 'no-cache');
    report.boot = { initial: boot.hash, final: again.hash, stable: boot.hash === again.hash, headers: boot.headers };
    const output = process.argv[3] || 'artifacts/production-integrity-probe.json';
    fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n');
    console.log(`${report.assets.length} remote hashes; ${report.assets.filter(a => !a.matches || a.status !== 200).length} failures; boot stable: ${report.boot.stable}; report: ${output}`);
    if (!report.boot.stable || report.assets.some(a => !a.matches || a.status !== 200) || report.probes.some(a => !a.matches || a.status !== 200)) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
