// Verify the actual runtime boot configuration, without executing generated JS.
// .NET 10 embeds it in dotnet.js; .NET 8/9 use blazor.boot.json.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const zlib = require('node:zlib');

function readBoot(framework) {
    const loader = path.join(framework, 'dotnet.js');
    const text = fs.existsSync(loader) ? fs.readFileSync(loader, 'utf8') : '';
    const embedded = text.match(/\/\*json-start\*\/([\s\S]*?)\/\*json-end\*\//);
    if (embedded) {
        if (fs.existsSync(path.join(framework, 'blazor.boot.json')))
            throw new Error('Mixed generations: embedded boot configuration and obsolete blazor.boot.json');
        return JSON.parse(embedded[1]);
    }
    return JSON.parse(fs.readFileSync(path.join(framework, 'blazor.boot.json'), 'utf8'));
}

function resourceFiles(resources) {
    const files = [];
    function visit(value, prefix = '') {
        if (Array.isArray(value)) {
            for (const item of value) {
                if (!item.name) throw new Error('Unrecognized boot resource');
                files.push({ name: prefix + item.name, hash: item.hash });
            }
        } else if (value && typeof value === 'object') {
            for (const [key, item] of Object.entries(value)) {
                if (typeof item === 'string' && item.startsWith('sha256-'))
                    files.push({ name: prefix + key, hash: item });
                else if (typeof item === 'object') visit(item, prefix + key + '/');
            }
        }
    }
    for (const [category, value] of Object.entries(resources)) {
        // resources.hash is an aggregate, not the digest of a file.
        if (category !== 'hash') visit(value);
    }
    if (!files.some(file => file.name.endsWith('.wasm') && file.hash))
        throw new Error('No integrity-protected WASM resources found; unknown boot format');
    return files;
}

function verify(root) {
    const framework = path.resolve(root, '_framework');
    const config = readBoot(framework);
    const files = resourceFiles(config.resources);
    let hashes = 0, compressed = 0;
    const allowed = new Set(['dotnet.js', 'blazor.webassembly.js', 'blazor.boot.json']);
    for (const { name, hash } of files) {
        if (/\.(wasm|dat|pdb)$/.test(name) && !hash) throw new Error(`Missing SRI: ${name}`);
        const file = path.resolve(framework, name);
        if (!file.startsWith(framework + path.sep)) throw new Error(`Unsafe resource path: ${name}`);
        const data = fs.readFileSync(file);
        allowed.add(name.replaceAll('\\', '/'));
        if (hash) {
            if (!/^sha256-[A-Za-z0-9+/]{43}=$/.test(hash)) throw new Error(`Invalid SRI: ${name}`);
            const actual = 'sha256-' + crypto.createHash('sha256').update(data).digest('base64');
            if (actual !== hash) throw new Error(`Integrity mismatch: ${name}; expected ${hash}, actual ${actual}`);
            hashes++;
        }
    }
    function scan(directory, prefix = '') {
        for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
            const name = prefix + entry.name, file = path.join(directory, entry.name);
            if (entry.isSymbolicLink()) throw new Error(`Unexpected symlink: ${name}`);
            if (entry.isDirectory()) { scan(file, name + '/'); continue; }
            const extension = path.extname(name);
            if (extension === '.gz' || extension === '.br') {
                const original = file.slice(0, -extension.length);
                const unpacked = extension === '.gz' ? zlib.gunzipSync(fs.readFileSync(file)) : zlib.brotliDecompressSync(fs.readFileSync(file));
                if (!unpacked.equals(fs.readFileSync(original))) throw new Error(`Compression mismatch: ${name}`);
                compressed++;
            } else if (!allowed.has(name) && !name.endsWith('.map')) {
                throw new Error(`Unreferenced framework asset (stale publish): ${name}`);
            }
        }
    }
    scan(framework);
    for (const name of ['dotnet.js', 'blazor.webassembly.js'])
        if (!fs.existsSync(path.join(framework, name))) throw new Error(`Missing startup loader: ${name}`);
    return { resources: files.length, hashes, compressed };
}

module.exports = { verify, readBoot, resourceFiles };
if (require.main === module) {
    try { console.log('Playground integrity OK:', verify(process.argv[2] || 'artifacts/playground')); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
}
