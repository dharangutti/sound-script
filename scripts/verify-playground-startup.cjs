// Browser regression against the real Release artifact, including actual SRI.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { verify } = require('./verify-playground-integrity.cjs');
const root = path.resolve(process.argv[2] || 'artifacts/site/playground');
const types = { '.html':'text/html', '.js':'text/javascript', '.wasm':'application/wasm', '.css':'text/css', '.json':'application/json', '.wav':'audio/wav', '.png':'image/png' };
let mode = 'normal', hits = 0;
const server = http.createServer((request, response) => {
    const url = new URL(request.url, 'http://localhost');
    const file = path.resolve(root, '.' + (url.pathname === '/' ? '/index.html' : decodeURIComponent(url.pathname)));
    if (!file.startsWith(root + path.sep)) { response.writeHead(403); response.end(); return; }
    if (/Microsoft\.Extensions\.DependencyInjection\.Abstractions\.[^.]+\.wasm$/.test(file)) {
        hits++;
        if (mode === '503' && hits === 1) { response.writeHead(503, { 'Content-Type':'text/html' }); response.end('Temporary upstream failure'); return; }
        if (mode === 'corrupt') { response.writeHead(200, { 'Content-Type':'application/wasm' }); response.end('wrong generation'); return; }
    }
    fs.readFile(file, (error, bytes) => {
        response.writeHead(error ? 404 : 200, { 'Content-Type':types[path.extname(file)] || 'application/octet-stream', 'Cache-Control':'max-age=600' });
        response.end(error ? 'Not found' : bytes);
    });
});
(async () => {
    verify(root);
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const url = `http://127.0.0.1:${server.address().port}/`;
    const browser = await chromium.launch({ headless:true, executablePath:process.env.CHROMIUM_PATH || undefined });
    try {
        for (const scenario of ['normal', '503', 'corrupt']) {
            mode = scenario; hits = 0;
            const context = await browser.newContext();
            const page = await context.newPage();
            const errors = []; page.on('pageerror', error => errors.push(error.message));
            const logs = []; page.on('console', message => { if (message.type() === 'error') logs.push(message.text()); });
            await page.goto(url);
            if (scenario === 'corrupt') {
                try { await page.locator('.boot-screen [role="alert"]').waitFor({timeout:30000}); }
                catch (error) { console.error({ hits, errors, logs }); throw error; }
                assert.equal(await page.locator('#music-workspace-tab').count(), 0, 'corrupt bytes must not start the app');
                assert.ok(hits >= 3 && hits <= 6, `bounded retries: ${hits}`);
            } else {
                await page.locator('#music-workspace-tab').waitFor({timeout:60000});
                for (const tab of ['visual', 'transcription', 'music']) {
                    await page.locator(`#${tab}-workspace-tab`).click();
                    assert.equal(await page.locator(`#${tab}-workspace-tab`).getAttribute('aria-selected'), 'true');
                }
                if (scenario === '503') assert.ok(hits >= 2, '503 must actually exercise retry');
                assert.equal(await page.evaluate(async () => (await navigator.serviceWorker.getRegistrations()).length), 0);
                const learning = page.getByRole('navigation', {name:'Learn SoundScript'});
                assert.equal(await learning.getByRole('link').count(), 4);
                await learning.getByRole('button', {name:'Try V16 adaptive media'}).click();
                await page.getByTestId('runtime-normal').waitFor();
                assert.equal(await page.locator('#visual-workspace-tab').getAttribute('aria-selected'), 'true');
                for (const link of await learning.getByRole('link').all()) {
                    const href = await link.getAttribute('href');
                    const reader = await context.newPage();
                    await reader.goto(new URL(href, url).href);
                    await reader.locator('#content h1').waitFor({timeout:10000});
                    assert.equal(await reader.locator('#content .error').count(), 0);
                    assert.ok((await reader.locator('#content').innerText()).length > 500);
                    if (href.includes('tutorials/')) {
                        await reader.getByRole('link', {name:'runtime API guide', exact:true}).click();
                        await reader.locator('#content h1').filter({hasText:'Programmable media runtime for .NET'}).waitFor();
                    }
                    await reader.close();
                }
                assert.deepEqual(errors, []);
                // Reload within the same context to exercise HTTP/Blazor cached resources.
                await page.reload();
                await page.locator('#music-workspace-tab').waitFor({timeout:60000});
                assert.deepEqual(errors, []);
            }
            console.log(`PASS browser startup: ${scenario}, target requests: ${hits}`);
            await context.close();
        }
    } finally { await browser.close(); server.close(); }
})().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
