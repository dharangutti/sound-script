const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.join(__dirname, 'site');
const types = {'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.mp4':'video/mp4','.webm':'video/webm'};
let broken = false;
const server = http.createServer((req,res) => {
    const url = new URL(req.url,'http://localhost');
    if (!url.pathname.startsWith('/labs/videolab/')) { res.writeHead(404); res.end(); return; }
    const relative = decodeURIComponent(url.pathname.slice('/labs/videolab/'.length)) || 'index.html';
    const file = path.resolve(root, relative);
    if (!file.startsWith(root + path.sep) || (broken && relative === 'proof.json')) { res.writeHead(404); res.end(); return; }
    fs.readFile(file, (error, data) => { res.writeHead(error ? 404 : 200, {'Content-Type':types[path.extname(file)] || 'text/plain'}); res.end(error ? 'Missing' : data); });
});
(async () => {
    await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
    const browser = await chromium.launch({headless:true});
    try {
        const page = await browser.newPage(); const errors = []; page.on('pageerror', e => errors.push(e.message));
        const url = `http://127.0.0.1:${server.address().port}/labs/videolab/`;
        await page.goto(url); await page.locator('#workspace').waitFor({state:'visible'});
        await page.waitForFunction(() => document.getElementById('video').readyState >= 2);
        await page.locator('#transition').click(); assert.match(await page.locator('#summary').innerText(), /2 visible clip/);
        await page.selectOption('#snapshot','1'); assert.match(await page.locator('#parameters').innerText(), /180/);
        assert.equal(await page.locator('#download').getAttribute('href'),'B.mp4');
        await page.selectOption('#format','webm');
        await page.waitForFunction(() => document.getElementById('video').readyState >= 2);
        await page.evaluate(() => document.getElementById('video').play());
        await page.waitForFunction(() => document.getElementById('video').currentTime > 0.1);
        assert.equal(await page.locator('#download').getAttribute('href'),'B.webm');
        await page.setViewportSize({width:390,height:844});
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),true);
        await page.screenshot({path:path.join(__dirname,'../artifacts/web-mobile.png'),fullPage:true});
        await page.setViewportSize({width:1280,height:900});
        await page.screenshot({path:path.join(__dirname,'../artifacts/web-desktop.png'),fullPage:true});
        assert.deepEqual(errors,[]);
        broken = true; await page.reload(); await page.locator('#error').waitFor({state:'visible'});
        assert.match(await page.locator('#error').innerText(),/Reload to retry/);
        console.log('PASS: subpath load, snapshot binding, crossfade frame, both codecs, playback, downloads, mobile layout, missing-data error.');
    } finally { await browser.close(); server.close(); }
})().catch(e => { console.error(e); server.close(); process.exitCode=1; });
