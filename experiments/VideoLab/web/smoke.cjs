const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const root = path.join(__dirname, 'site');
const types = {'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.mp4':'video/mp4','.webm':'video/webm','.jpg':'image/jpeg'};
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
    const browser=await chromium.launch({headless:true});
    try {
        const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
        const url=`http://127.0.0.1:${server.address().port}/labs/videolab/`;
        await page.goto(url);await page.locator('#workspace').waitFor({state:'visible'});
        await page.waitForFunction(()=>document.getElementById('video').readyState>=2);
        assert.equal(await page.locator('.card').count(),9);
        assert.match(await page.locator('#demo-title').innerText(),/Video \+ audio/);
        assert.equal(await page.locator('.track-row').count(),4);
        assert.match(await page.locator('#timeline').textContent(),/Crossfade/);
        await page.evaluate(()=>document.getElementById('video').play());await page.waitForFunction(()=>document.getElementById('video').currentTime>0.1);
        const expected=JSON.parse(fs.readFileSync(path.join(root,'proof.json')));
        for(const demo of expected.demos){
            await page.locator(`.card[data-id="${demo.id}"] button`).click();
            assert.equal(await page.locator('#frame').getAttribute('max'),String(demo.frames-1));
            for(const snap of demo.snapshots){
                if(demo.id==='showcase'){await page.selectOption('#sample-video',snap.video);await page.selectOption('#sample-audio',snap.audio);}
                if(await page.locator('#snapshot option').count()>1)await page.selectOption('#snapshot',String(demo.snapshots.filter(s=>s.video===snap.video&&s.audio===snap.audio).findIndex(s=>s.name===snap.name)));
                for(const format of ['mp4','webm']){
                    await page.selectOption('#format',format);await page.waitForFunction(()=>document.getElementById('video').readyState>=2);
                    const file=`${snap.stem}.${format}`;assert.equal(await page.locator(`#export-${format}`).getAttribute('href'),file);
                    assert.equal((await page.request.get(new URL(file,url).href)).status(),200);
                }
                await page.locator('#transition').click();assert.deepEqual(JSON.parse(await page.locator('#scene').textContent()),snap.scenes[demo.inspectFrame]);
                assert.deepEqual(JSON.parse(await page.locator('#script').textContent()),snap.script);
                if(demo.id==='audience'){
                    assert.equal(await page.locator('#parameter-audience').inputValue(),snap.name);
                    assert.equal(await page.locator('#parameter-audience').isDisabled(),true);
                    assert.ok(snap.scenes[demo.inspectFrame].Layers.filter(l=>l.Group&&l.Included).every(l=>l.Group===snap.name));
                }
                if(demo.id==='editing'){
                    assert.equal(await page.locator('.track-row').count(),6);
                    assert.match(await page.locator('#edit-properties').textContent(),/source trim 10/);
                    assert.match(await page.locator('#edit-properties').textContent(),/DejaVu Sans 2.37/);
                    assert.match(await page.locator('#edit-properties').textContent(),/rotation/);
                }
            }
        }
        await page.locator('.card[data-id="showcase"] button').click();await page.selectOption('#snapshot','1');await page.locator('#reset-parameters').click();assert.equal(await page.locator('#snapshot').inputValue(),'0');
        assert.equal(await page.locator('#parameter-accentX').isDisabled(),true);
        await page.locator('#personal-mode').click();assert.equal(await page.locator('#local-launch').isVisible(),true);assert.equal(await page.locator('#file-controls').isVisible(),false);
        await page.locator('#sample-mode').click();assert.equal(await page.locator('#sample-panel').isVisible(),true);
        await page.locator('#transition').click();await page.waitForFunction(()=>!document.getElementById('video').seeking&&Math.abs(document.getElementById('video').currentTime-98/30)<0.02);
        await page.locator('#frame').focus();await page.keyboard.press('ArrowRight');assert.equal(await page.locator('#frame').inputValue(),'99');
        for(const width of [390,768,1360]){await page.setViewportSize({width,height:900});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);}
        await page.reload();await page.waitForFunction(()=>document.getElementById('video').readyState>=2);
        await page.evaluate(()=>document.getElementById('video').play());await page.waitForFunction(()=>document.getElementById('video').currentTime>=0.5);await page.evaluate(()=>document.getElementById('video').pause());
        await page.screenshot({path:path.join(__dirname,'../artifacts/ui-desktop.png'),fullPage:true});
        await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(__dirname,'../artifacts/ui-mobile.png'),fullPage:true});
        assert.deepEqual(errors,[]);
        broken=true;await page.reload();await page.locator('#error').waitFor({state:'visible'});assert.match(await page.locator('#error').innerText(),/Reload to retry/);
        // All prior assertions remain, with the eighth demo and richer track set.
        console.log('PASS: nine demo cards, all sample/binding/format combinations, native playback, exact scenes, text/callout/transition lanes, editing properties, source sync, public-mode boundaries, reset, keyboard seek, 3 responsive widths and missing-data errors.');
    }finally{await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
