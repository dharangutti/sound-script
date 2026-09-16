const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/SoundScript.Playground/wwwroot/js/playground-startup.js'), 'utf8');
function loader(fetch) {
    const context = { window: {}, fetch, AbortSignal, setTimeout: callback => callback(), Blazor: { start: async () => {} },
        console: { error() {} }, document: { querySelector() { return null; }, getElementById() { return null; } } };
    vm.runInNewContext(source, context);
    return context.window.SoundScriptStartup.loadBootResource;
}
test('transient 503 recovers with the same SRI and cache reload', async () => {
    const requests = [];
    const load = loader(async (uri, options) => { requests.push(options); return { ok: requests.length > 1, status: requests.length > 1 ? 200 : 503 }; });
    assert.equal((await load('assembly', 'App.wasm', '/App.wasm', 'sha256-expected')).status, 200);
    assert.deepEqual(requests.map(r => [r.integrity, r.cache]), [['sha256-expected','default'], ['sha256-expected','reload']]);
});
test('SRI/network rejection is retried but never bypassed', async () => {
    let calls = 0;
    const load = loader(async (_, options) => { calls++; assert.equal(options.integrity, 'sha256-expected'); throw new TypeError('Failed to fetch'); });
    await assert.rejects(load('assembly', 'App.wasm', '/App.wasm', 'sha256-expected'), /Failed to fetch/);
    assert.equal(calls, 3);
});
test('permanent missing resource fails without repeated requests', async () => {
    let calls = 0; const load = loader(async () => { calls++; return { ok: false, status: 404 }; });
    await assert.rejects(load('assembly', 'App.wasm', '/App.wasm', 'sha256-expected'), /HTTP 404/);
    assert.equal(calls, 1);
});
test('JS modules and resources without integrity retain runtime loading', () => {
    const load = loader(() => { throw new Error('must not fetch'); });
    assert.equal(load('dotnetjs', 'dotnet.js', '/dotnet.js', 'hash'), null);
    assert.equal(load('manifest', 'boot.json', '/boot.json', ''), null);
});
