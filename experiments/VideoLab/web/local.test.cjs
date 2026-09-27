// Integration coverage for the real native adapter, including cleanup and FFmpeg.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const { spawn, execFileSync } = require('node:child_process');
const fs = require('node:fs'), path = require('node:path'), os = require('node:os'), assert = require('node:assert/strict');
const lab = path.resolve(__dirname, '..'), origin = 'http://127.0.0.1:18745';
const before = new Set(fs.readdirSync(os.tmpdir()).filter(n => n.startsWith('videolab-session-')));
const child = spawn('dotnet', [process.env.VIDEOLAB_DLL || 'bin/Release/net10.0/VideoLab.dll', 'serve', '18745'], {cwd:lab, stdio:['ignore','pipe','pipe']});
let log = ''; child.stdout.on('data', d => log += d); child.stderr.on('data', d => log += d);
const exited = new Promise(resolve => child.on('exit', resolve));
let token, browser;
const state = {demo:'showcase',media:'personal',video:'montage',audio:'calm',parameters:{accentX:80,musicGain:0.2,clipGain:0.1}};
async function api(route, body=state, extra={}) {
    const r = await fetch(origin+'/api/'+route,{method:'POST',headers:{Origin:origin,'X-VideoLab-Session':token,'Content-Type':'application/json',...extra.headers},body:extra.file || JSON.stringify(body)});
    return {status:r.status,data:await r.json()};
}
async function upload(kind, filename, bytes) { return api('upload?kind='+kind+'&demo=showcase',null,{headers:{'X-File-Name':encodeURIComponent(filename)},file:bytes}); }
(async()=>{
    try {
        for(let i=0;i<100;i++) { try { const r=await fetch(origin+'/api/session');token=(await r.json()).token;break; } catch { await new Promise(r=>setTimeout(r,100)); } }
        assert.ok(token,log);
        assert.equal((await fetch(origin+'/api/session',{headers:{Origin:'https://example.com'}})).status,403);
        assert.equal((await fetch(origin+'/api/bind',{method:'POST',body:'{}'})).status,403);
        const audienceState={demo:'audience',media:'samples',video:'original',audio:'original',parameters:{audience:'qa',showSafety:false}};
        const audienceBound=await api('bind',audienceState);assert.equal(audienceBound.status,200);assert.equal(audienceBound.data.parameters.audience,'qa');
        assert.equal((await api('bind',{...audienceState,parameters:{audience:'unknown'}})).status,400);
        assert.equal((await api('bind',{...audienceState,parameters:{showSafety:'true'}})).status,400);
        const annotationState={...audienceState,demo:'annotations',annotationSet:'qa',parameters:{audience:'qa'}};
        const annotationBound=await api('bind',annotationState);assert.equal(annotationBound.status,200);
        assert.equal(annotationBound.data.annotationData.annotations[0].id,'qa_note');assert.equal(annotationBound.data.baseScript.texts.length,1);
        assert.equal((await api('bind',{...annotationState,annotationSet:'../../secret'})).status,400);
        const bound = await api('bind'); assert.equal(bound.status,200);assert.equal(bound.data.parameters.accentX,80);
        assert.equal((await api('bind',{...state,parameters:{accentX:99999}})).status,400);
        assert.deepEqual((await api('bind')).data.scenes,bound.data.scenes);
        assert.equal((await upload('video','bad.exe',Buffer.from('bad'))).status,400);
        assert.equal((await upload('video','bad.mp4',Buffer.from('bad'))).status,400);
        assert.equal((await upload('video','empty.mp4',Buffer.alloc(0))).status,400);
        assert.equal((await upload('video','large.mp4',Buffer.alloc(50*1024*1024+1))).status,400);
        execFileSync('ffmpeg',['-v','error','-y','-i','web/samples/pattern.mp4','-t','0.1','artifacts/local-short.mp4'],{cwd:lab});
        assert.equal((await upload('video','short.mp4',fs.readFileSync(path.join(lab,'artifacts/local-short.mp4')))).status,400);
        for(const ext of ['mp4','webm']) assert.equal((await upload('video','chosen.'+ext,fs.readFileSync(path.join(lab,'artifacts/A.'+ext)))).status,200);
        assert.equal((await upload('audio','chosen.wav',fs.readFileSync(path.join(lab,'web/samples/calm.wav')))).status,200);
        execFileSync('ffmpeg',['-v','error','-y','-i','web/samples/calm.wav','artifacts/local-test.mp3'],{cwd:lab});
        assert.equal((await upload('audio','chosen.mp3',fs.readFileSync(path.join(lab,'artifacts/local-test.mp3')))).status,200);
        const personal = await api('bind'); assert.match(JSON.stringify(personal.data.script),/uploads\//); assert.doesNotMatch(JSON.stringify(personal.data),/videolab-session-|[A-Z]:\\/);
        assert.equal((await upload('video','replacement.mp4',Buffer.from('bad'))).status,400);
        assert.deepEqual((await api('bind')).data.script,personal.data.script);
        let hash, result;
        for(const format of ['mp4','mp4','webm']) {
            result = await api('render',{...state,format});assert.equal(result.status,200,JSON.stringify(result));
            if(format==='mp4'){ if(hash)assert.equal(result.data.sha256,hash);hash=result.data.sha256; }
            const bytes = await (await fetch(origin+result.data.url)).arrayBuffer();
            const file=path.join(lab,'artifacts/local-result.'+format);fs.writeFileSync(file,Buffer.from(bytes));
            execFileSync('ffmpeg',['-v','error','-xerror','-i',file,'-f','null','-']);
        }
        const range=await fetch(origin+result.data.url,{headers:{Range:'bytes=0-31'}});assert.equal(range.status,206);assert.equal((await range.arrayBuffer()).byteLength,32);
        const rendering=api('render',{...state,format:'webm'});await new Promise(r=>setTimeout(r,350));await api('cancel');assert.equal((await rendering).status,408);
        assert.equal((await api('reset')).status,200);assert.equal((await fetch(origin+result.data.url)).status,404);
        assert.doesNotMatch(JSON.stringify((await api('bind')).data.script),/uploads\//);
        browser=await chromium.launch({headless:true});const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
        await page.goto(origin+'/labs/videolab/');await page.locator('#workspace').waitFor({state:'visible'});
        await page.locator('#parameter-accentX').fill('100');await page.locator('#parameter-accentX').dispatchEvent('change');
        await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).accentX===100);
        await page.locator('#personal-mode').click();await page.locator('#video-file').setInputFiles(path.join(lab,'artifacts/A.mp4'));
        await page.waitForFunction(()=>document.getElementById('video-file-info').textContent.includes('A.mp4'));
        await page.locator('#audio-file').setInputFiles(path.join(lab,'web/samples/calm.wav'));
        await page.waitForFunction(()=>document.getElementById('audio-file-info').textContent.includes('calm.wav'));
        await page.locator('#render').click();await page.waitForFunction(()=>document.getElementById('render-state').textContent==='Current composition rendered',null,{timeout:120000});
        assert.match(await page.locator('#export-mp4').getAttribute('href'),/^\/results\//);
        await page.locator('#sample-mode').click();await page.waitForFunction(()=>!document.getElementById('script').textContent.includes('uploads/'));
        assert.match(await page.locator('#video-file-info').textContent(),/No personal/);assert.deepEqual(errors,[]);
        await page.locator('.card[data-id="editing"] button').click();
        await page.waitForFunction(()=>document.getElementById('script').textContent.includes('Composition review'));
        assert.equal(await page.locator('#sample-video').inputValue(),'original');
        assert.equal(await page.locator('#parameter-rotation').isEnabled(),true);
        await page.locator('#parameter-position').fill('20');await page.locator('#parameter-position').dispatchEvent('change');
        await page.locator('#parameter-rotation').fill('5');await page.locator('#parameter-rotation').dispatchEvent('change');
        await page.locator('#transition').click();
        await page.waitForFunction(()=>JSON.parse(document.getElementById('scene').textContent).Layers.find(l=>l.Kind==='video').Transform.Rotation===5);
        assert.equal(await page.locator('.track-row').count(),6);
        await page.locator('#render').click();await page.waitForFunction(()=>document.getElementById('render-state').textContent==='Current composition rendered',null,{timeout:120000});
        await page.waitForFunction(()=>document.getElementById('video').readyState>=2);await page.locator('#transition').click();
        await page.waitForFunction(()=>!document.getElementById('video').seeking);
        await page.screenshot({path:path.join(lab,'artifacts/phase1-local-desktop.png'),fullPage:true});
        await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
        await page.screenshot({path:path.join(lab,'artifacts/phase1-local-mobile.png'),fullPage:true});
        assert.deepEqual(errors,[]);
        await page.locator('.card[data-id="audience"] button').click();
        await page.locator('#parameter-audience').selectOption('qa');
        await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).audience==='qa');
        await page.locator('#transition').click();
        assert.ok(JSON.parse(await page.locator('#scene').textContent()).Layers.filter(l=>l.Group&&l.Included).every(l=>l.Group==='qa'));
        await page.locator('#parameter-showSafety').selectOption('false');
        await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).showSafety===false);
        await page.locator('#render').click();await page.waitForFunction(()=>document.getElementById('render-state').textContent==='Current composition rendered',null,{timeout:120000});
        await page.setViewportSize({width:1280,height:900});await page.evaluate(()=>scrollTo(0,0));
        await page.waitForFunction(()=>document.getElementById('video').readyState>=2);await page.locator('#transition').click();await page.waitForFunction(()=>!document.getElementById('video').seeking);
        await page.screenshot({path:path.join(lab,'artifacts/phase2-local.png')});
        await page.locator('#reset-parameters').click();
        await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).audience==='shopfloor');
        assert.equal(await page.locator('#parameter-showSafety').inputValue(),'true');
        assert.deepEqual(errors,[]);
        await page.locator('.card[data-id="annotations"] button').click();await page.selectOption('#snapshot','1');
        await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).audience==='qa');
        assert.equal(JSON.parse(await page.locator('#annotations-json').textContent()).annotations[0].id,'qa_note');
        assert.equal(JSON.parse(await page.locator('#script').textContent()).texts.length,1);
        await page.locator('#render').click();await page.waitForFunction(()=>document.getElementById('render-state').textContent==='Current composition rendered',null,{timeout:120000});
        await page.waitForFunction(()=>document.getElementById('video').readyState>=2);await page.locator('#transition').click();await page.waitForFunction(()=>!document.getElementById('video').seeking);
        // Let a decoded frame reach the compositor before capturing the preview.
        await page.evaluate(()=>document.getElementById('video').play());
        await page.waitForFunction(()=>document.getElementById('video').currentTime>1.1);
        await page.evaluate(()=>{document.getElementById('video').pause();scrollTo(0,0);});
        await page.screenshot({path:path.join(lab,'artifacts/phase3-local.png')});
        await page.selectOption('#snapshot','2');await page.waitForFunction(()=>JSON.parse(document.getElementById('parameters').textContent).audience==='engineering');
        assert.equal(JSON.parse(await page.locator('#annotations-json').textContent()).annotations[0].id,'engineering_note');
        assert.deepEqual(errors,[]);
        await browser.close();browser=null;
        await api('stop');assert.equal(await exited,0,log);
        const leftover=fs.readdirSync(os.tmpdir()).filter(n=>n.startsWith('videolab-session-')&&!before.has(n));assert.deepEqual(leftover,[]);
        console.log('PASS: local origin/token checks, atomic runtime binding, MP4/WebM/WAV/MP3 selection, malformed/type/size validation, replacement preservation, repeated MP4, WebM decode, ranges, cancellation, browser controls/render/reset, shutdown cleanup.');
    } finally { if(browser)await browser.close();if(child.exitCode===null){try{await api('stop');await exited;}catch{child.kill();}} }
})().catch(e=>{console.error(e);console.error(log);process.exitCode=1;});
