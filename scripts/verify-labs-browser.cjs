const {chromium} = require('playwright');
const fs = require('node:fs'), path = require('node:path'), http = require('node:http'), assert = require('node:assert/strict');
const root = path.resolve(process.argv[2] || 'artifacts/site');
const manifest = require('../docs/labs-manifest.json');
const types = {'.html':'text/html','.css':'text/css','.js':'text/javascript','.json':'application/json','.mp4':'video/mp4','.webm':'video/webm','.wasm':'application/wasm'};
const server = http.createServer((req,res) => {
    const pathname = decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const file = path.resolve(root, '.' + pathname + (pathname.endsWith('/') ? 'index.html' : ''));
    if (!file.startsWith(root + path.sep)) {res.writeHead(403);res.end();return;}
    fs.readFile(file,(error,data) => {res.writeHead(error?404:200,{'Content-Type':types[path.extname(file)] || 'application/octet-stream'});res.end(error?'Missing':data);});
});
(async () => {
    await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
    const browser = await chromium.launch({headless:true});
    try {
        const base = `http://127.0.0.1:${server.address().port}`, page = await browser.newPage();
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        page.on('response', r => {if(r.status() >= 400) errors.push(`${r.status()} ${r.url()}`);});
        await page.goto(base + '/labs/');
        assert.equal(await page.locator('article').count(),manifest.labs.length);
        for(const lab of manifest.labs) {
            const route = `/labs/${lab.id}/`;
            if(!lab.publish) {assert.equal((await page.request.get(base+route)).status(),404);continue;}
            const escaped = [];
            const listener = request => {if(new URL(request.url()).origin !== base || !new URL(request.url()).pathname.startsWith(route)) escaped.push(request.url());};
            page.on('request',listener);
            await page.goto(base+route); await page.waitForLoadState('networkidle');
            assert.equal(await page.locator('h1').count(),1);
            assert.ok((await page.locator('body').innerText()).includes(lab.name));
            for(const href of await page.locator('a[href]').evaluateAll(links => links.map(a=>a.href))) {
                if(href.startsWith(base+route)) assert.equal((await page.request.get(href)).status(),200,href);
            }
            assert.deepEqual(escaped,[],`${lab.id}: resources escaped subpath`);
            page.off('request',listener);
            await page.setViewportSize({width:390,height:844});
            assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth <= innerWidth),true);
            await page.goto(base+'/labs/');
        }
        assert.deepEqual(errors,[]); console.log('PASS: Labs catalog, approved routes, withheld routes, links, subpath requests and mobile browser smoke.');
    } finally {await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
