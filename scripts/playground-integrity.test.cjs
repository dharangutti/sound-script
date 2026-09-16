const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const zlib = require('node:zlib');
const { verify } = require('./verify-playground-integrity.cjs');

function fixture(t, legacy = false) {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'playground-integrity-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    const framework = path.join(root, '_framework'); fs.mkdirSync(framework);
    const data = Buffer.from([0, 97, 115, 109, 13, 10, 255, 0]);
    const hash = 'sha256-' + crypto.createHash('sha256').update(data).digest('base64');
    const config = { resources: { hash: 'aggregate-is-not-a-file', assembly: legacy ? { 'App.wasm': hash } : [{ name: 'App.wasm', hash }] } };
    fs.writeFileSync(path.join(framework, 'App.wasm'), data);
    fs.writeFileSync(path.join(framework, 'App.wasm.gz'), zlib.gzipSync(data));
    fs.writeFileSync(path.join(framework, 'App.wasm.br'), zlib.brotliCompressSync(data));
    fs.writeFileSync(path.join(framework, 'blazor.webassembly.js'), '// loader');
    fs.writeFileSync(path.join(framework, 'dotnet.js'), legacy ? '// loader' : `/*json-start*/${JSON.stringify(config)}/*json-end*/`);
    if (legacy) fs.writeFileSync(path.join(framework, 'blazor.boot.json'), JSON.stringify(config));
    return { root, framework, config };
}
test('verifies embedded .NET 10 boot metadata and compressed variants', t => {
    assert.deepEqual(verify(fixture(t).root), { resources: 1, hashes: 1, compressed: 2 });
});
test('also verifies legacy JSON boot manifests', t => { assert.equal(verify(fixture(t, true).root).hashes, 1); });
test('fails when bytes differ, including line-ending transformation', t => {
    const { root, framework } = fixture(t);
    fs.appendFileSync(path.join(framework, 'App.wasm'), '\r\n');
    assert.throws(() => verify(root), /Integrity mismatch/);
});
test('fails missing referenced files', t => {
    const { root, framework } = fixture(t); fs.unlinkSync(path.join(framework, 'App.wasm'));
    assert.throws(() => verify(root), /ENOENT/);
});
test('fails compressed variants from a different generation', t => {
    const { root, framework } = fixture(t);
    fs.writeFileSync(path.join(framework, 'App.wasm.br'), zlib.brotliCompressSync(Buffer.from('old build')));
    assert.throws(() => verify(root), /Compression mismatch/);
});
test('fails stale framework files and obsolete boot manifests', t => {
    const { root, framework } = fixture(t);
    fs.writeFileSync(path.join(framework, 'Old.123.wasm'), 'old');
    assert.throws(() => verify(root), /Unreferenced/);
    fs.writeFileSync(path.join(framework, 'blazor.boot.json'), '{}');
    assert.throws(() => verify(root), /Mixed generations/);
});
test('fails closed on unknown boot format', t => {
    const { root, framework } = fixture(t);
    fs.writeFileSync(path.join(framework, 'dotnet.js'), '/*json-start*/{"resources":{}}/*json-end*/');
    assert.throws(() => verify(root), /No integrity-protected/);
});
test('verifies nested satellite resource paths', t => {
    const { root, framework, config } = fixture(t);
    fs.mkdirSync(path.join(framework, 'fr'));
    fs.copyFileSync(path.join(framework, 'App.wasm'), path.join(framework, 'fr/App.resources.wasm'));
    config.resources.satelliteResources = { fr: [{ name: 'App.resources.wasm', hash: config.resources.assembly[0].hash }] };
    fs.writeFileSync(path.join(framework, 'dotnet.js'), `/*json-start*/${JSON.stringify(config)}/*json-end*/`);
    assert.equal(verify(root).hashes, 2);
});
test('does not silently accept a WASM resource without integrity', t => {
    const { root, framework, config } = fixture(t);
    config.resources.assembly.push({ name: 'Unchecked.wasm' });
    fs.writeFileSync(path.join(framework, 'dotnet.js'), `/*json-start*/${JSON.stringify(config)}/*json-end*/`);
    assert.throws(() => verify(root), /Missing SRI/);
});
