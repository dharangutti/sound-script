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
    fs.readFile(file, (error, data) => {
        if (error) { res.writeHead(404); res.end('Missing'); return; }
        const headers = {'Content-Type':types[path.extname(file)] || 'text/plain', 'Accept-Ranges':'bytes'};
        const range = /^bytes=(\d+)-(\d*)$/.exec(req.headers.range || '');
        if (range) {
            const start = Number(range[1]), end = Math.min(range[2] ? Number(range[2]) : data.length - 1, data.length - 1);
            if (start > end) { res.writeHead(416); res.end(); return; }
            res.writeHead(206, {...headers, 'Content-Range':`bytes ${start}-${end}/${data.length}`, 'Content-Length':end-start+1});
            res.end(data.subarray(start,end+1));
        } else { res.writeHead(200, {...headers, 'Content-Length':data.length}); res.end(data); }
    });
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
        const expected = JSON.parse(fs.readFileSync(path.join(root, 'proof.json')));
        assert.equal(await page.locator('#demo option').count(), 6);
        for (const demo of expected.demos) {
            await page.selectOption('#demo', demo.id);
            assert.equal(await page.locator('#frame').getAttribute('max'), String(demo.frames - 1));
            assert.equal(await page.locator('#snapshot').isDisabled(), demo.snapshots.length === 1);
            for (let i = 0; i < demo.snapshots.length; i++) {
                if (demo.snapshots.length > 1) await page.selectOption('#snapshot', String(i));
                for (const format of ['mp4', 'webm']) {
                    await page.selectOption('#format', format);
                    await page.waitForFunction(() => document.getElementById('video').readyState >= 2);
                    const file = `${demo.snapshots[i].stem}.${format}`;
                    assert.equal(await page.locator('#download').getAttribute('href'), file);
                    assert.equal((await page.request.get(new URL(file, url).href)).status(), 200);
                    assert.equal(await page.locator('#error').isVisible(), false);
                }
                await page.locator('#transition').click();
                assert.deepEqual(JSON.parse(await page.locator('#scene').innerText()), demo.snapshots[i].scenes[demo.inspectFrame]);
            }
        }
        await page.selectOption('#demo', 'conditional'); await page.selectOption('#snapshot', '1');
        assert.equal(JSON.parse(await page.locator('#scene').innerText()).Layers[0].Included, false);
        await page.locator('#transition').click();
        assert.equal(JSON.parse(await page.locator('#scene').innerText()).Layers[1].Included, true);
        await page.selectOption('#demo', 'transforms'); await page.locator('#transition').click();
        assert.equal(JSON.parse(await page.locator('#scene').innerText()).Audio[0].Gain, 0.6);
        await page.locator('#frame').focus(); await page.keyboard.press('ArrowRight');
        assert.equal(await page.locator('#frame').inputValue(), '16');
        await page.selectOption('#demo', 'effects'); await page.locator('#transition').click();
        await page.waitForFunction(() => {
            const video = document.getElementById('video');
            return !video.seeking && video.readyState >= 2 && Math.abs(video.currentTime - 0.5) < 0.01;
        });
        await page.setViewportSize({width:390,height:844});
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),true);
        await page.screenshot({path:path.join(__dirname,'../artifacts/web-mobile.png'),fullPage:true});
        await page.setViewportSize({width:1280,height:900});
        await page.screenshot({path:path.join(__dirname,'../artifacts/web-desktop.png'),fullPage:true});
        assert.deepEqual(errors,[]);
        broken = true; await page.reload(); await page.locator('#error').waitFor({state:'visible'});
        assert.match(await page.locator('#error').innerText(),/Reload to retry/);
        console.log('PASS: six demos, ten bindings, twenty media files, exact scene data, conditions, audio gain, keyboard frame control, subpath playback/downloads, mobile and missing-data error.');
    } finally { await browser.close(); server.close(); }
})().catch(e => { console.error(e); server.close(); process.exitCode=1; });
