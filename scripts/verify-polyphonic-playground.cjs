// Run after a Release publish and a Debug CLI build. No third-party media.
const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const analysisMode = process.argv[3] || 'polyphonic';
assert.ok(['polyphonic','mixed','percussion'].includes(analysisMode));
const browserMode = analysisMode === 'percussion' ? 'Percussion' : analysisMode === 'mixed' ? 'Mixed' : 'Polyphonic';
const root = path.resolve(process.argv[2] || 'artifacts/polyphonic-playground');
const output = path.resolve(`artifacts/${analysisMode}-browser`); fs.mkdirSync(output, { recursive: true });
const types = { '.html':'text/html', '.js':'text/javascript', '.wasm':'application/wasm', '.css':'text/css', '.json':'application/json' };
const server = http.createServer((req,res) => {
    const url = new URL(req.url,'http://localhost'), file = path.resolve(root,'.'+(url.pathname==='/'?'/index.html':decodeURIComponent(url.pathname)));
    if(!file.startsWith(root+path.sep)){res.writeHead(403);res.end();return;}
    fs.readFile(file,(err,bytes)=>{res.writeHead(err?404:200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});res.end(err?'Not found':bytes);});
});
function fixture(name, seconds, noise=false) {
    const count = seconds*16000, data=Buffer.alloc(44+count*2);
    data.write('RIFF');data.writeUInt32LE(data.length-8,4);data.write('WAVEfmt ',8);data.writeUInt32LE(16,16);
    data.writeUInt16LE(1,20);data.writeUInt16LE(1,22);data.writeUInt32LE(16000,24);data.writeUInt32LE(32000,28);
    data.writeUInt16LE(2,32);data.writeUInt16LE(16,34);data.write('data',36);data.writeUInt32LE(count*2,40);
    let seed=17;
    for(let i=0;i<count;i++) {
        const t=i/16000, local=t%1;let sample=0;
        seed=(1664525*seed+1013904223)>>>0;
        if(noise) sample=(seed/4294967296*2-1)*.5;
        else if(analysisMode==='percussion') {
            if(local>=.2) sample=.55*Math.sin(2*Math.PI*(65*(local-.2)+.7*(1-Math.exp(-(local-.2)*50))))*Math.exp(-(local-.2)*25);
        }
        else if(local>=.2&&local<.8) for(const pitch of (analysisMode === 'mixed' ? [43,60,65,76] : [60,64,67])) {
            const phase=2*Math.PI*440*2**((pitch-69)/12)*(local-.2);
            sample+=.18*(Math.sin(phase)+.33*Math.sin(2*phase)+.16*Math.sin(3*phase))*Math.min(1,(local-.2)/.008)*Math.min(1,(.8-local)/.015)*Math.exp(-(local-.2)*1.2);
        }
        data.writeInt16LE(Math.round(sample*32767),44+i*2);
    }
    const file=path.join(output,name+'.wav');fs.writeFileSync(file,data);return file;
}
(async()=>{
    const triad=fixture('triad',2),noise=fixture('noise',2,true),long=fixture('long',30);
    const cliSource=path.join(output,'cli.ss'),cliReport=path.join(output,'cli.json');
    execFileSync('dotnet',['src/SoundScript.Cli/bin/Debug/net10.0/soundscript.dll','transcribe',triad,'--mode',analysisMode,'--tempo','120','--out',cliSource,'--report',cliReport],{stdio:'pipe'});
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    const browser=await chromium.launch({headless:true,executablePath:process.env.CHROMIUM_PATH||undefined});
    try {
        const page=await browser.newPage({acceptDownloads:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
        await page.goto(`http://127.0.0.1:${server.address().port}`);
        await page.locator('#transcription-workspace-tab').click({timeout:60000});
        const analyze=page.getByRole('button',{name:'Analyze / Transcribe',exact:true});
        const exportSource=page.getByRole('button',{name:'Export SoundScript',exact:true});
        const ready=()=>page.waitForFunction(()=>document.querySelector('.transcription-workspace [role="status"]').textContent.includes('Media ready'));
        const finish=()=>page.waitForFunction(()=>!document.querySelector('#transcription-file').disabled,null,{timeout:60000});
        await page.locator('#transcription-file').setInputFiles(triad);await ready();
        await page.locator('#transcription-mode').selectOption(browserMode);
        if(analysisMode==='percussion') assert.equal(await page.locator('#transcription-instrument').isDisabled(),true);
        else assert.equal(await page.locator('#transcription-instrument').inputValue(),'0');
        await page.locator('#transcription-tempo').fill('120');await page.locator('#transcription-tempo').blur();
        await analyze.click();await finish();
        assert.equal(await exportSource.isEnabled(),true);
        assert.equal(await page.locator('#transcription-source').inputValue(),fs.readFileSync(cliSource,'utf8'));
        if(analysisMode==='percussion') assert.match(await page.locator('.transcription-workspace').innerText(),/2 detected hits/);
        else assert.match(await page.locator('.transcription-workspace').innerText(),new RegExp('maximum simultaneous notes '+(analysisMode==='mixed'?4:3)));
        const downloadReport=page.waitForEvent('download');await page.getByRole('button',{name:'Export analysis',exact:true}).click();
        const report=await downloadReport;const reportPath=path.join(output,'browser.json');await report.saveAs(reportPath);
        const browserReport=JSON.parse(fs.readFileSync(reportPath,'utf8')), cli=JSON.parse(fs.readFileSync(cliReport,'utf8'));
        // WASM and desktop FFT arithmetic can differ below 1e-12 in evidence.
        // Require exact source, pitches, timing and voices; bound only evidence drift.
        const browserScore=browserReport.Transcription.Score, cliScore=cli.Transcription.Score;
        for(let t=0;t<cliScore.Tracks.length;t++)for(let n=0;n<cliScore.Tracks[t].Notes.length;n++) {
            const a=browserScore.Tracks[t].Notes[n].PitchEvidence,b=cliScore.Tracks[t].Notes[n].PitchEvidence;
            assert.ok(Math.abs(a.Confidence-b.Confidence)<=1e-12,'evidence drift exceeds cross-runtime tolerance');
            a.Confidence=b.Confidence;
        }
        if(analysisMode==='percussion') {
            for(let t=0;t<cliScore.Tracks.length;t++)for(let n=0;n<cliScore.Tracks[t].Percussion.length;n++) {
                const a=browserScore.Tracks[t].Percussion[n],b=cliScore.Tracks[t].Percussion[n];
                for(const key of ['OnsetStrength','LowShare','MidShare','HighShare']) { assert.ok(Math.abs(a[key]-b[key])<=1e-12);a[key]=b[key]; }
                assert.ok(Math.abs(a.Classification.Confidence-b.Classification.Confidence)<=1e-12);a.Classification.Confidence=b.Classification.Confidence;
            }
            assert.ok(cliScore.Tracks.every(t=>t.Notes.length===0));
        }
        assert.deepEqual(browserScore,cliScore);
        if(analysisMode!=='percussion') assert.equal(browserReport.Transcription.Polyphony.MaximumSimultaneousNotes,analysisMode==='mixed'?4:3);
        if(analysisMode==='mixed') {
            await page.locator('#role-harmony').uncheck();assert.equal(await exportSource.isEnabled(),false);
            await analyze.click();await finish();
            const selected=await page.locator('#transcription-source').inputValue();
            assert.match(selected,/track bass/);assert.match(selected,/track melody/);assert.doesNotMatch(selected,/track harmony/);
            await page.getByRole('button',{name:'Play analyzed bass',exact:true}).click();await finish();
            await page.getByRole('button',{name:'Stop',exact:true}).click();
            await page.locator('#role-harmony').check();await analyze.click();await finish();
        }
        await page.locator('#transcription-start').fill('0.2');await page.locator('#transcription-start').blur();
        await page.locator('#transcription-duration').fill('0.4');await page.locator('#transcription-duration').blur();
        assert.equal(await exportSource.isEnabled(),false);
        await page.getByRole('button',{name:'Play original excerpt',exact:true}).click();
        await page.waitForFunction(()=>{const a=document.querySelector('#transcription-original');return a.paused&&a.currentTime>=.6;});
        const stoppedAt=await page.locator('#transcription-original').evaluate(a=>a.currentTime);
        assert.ok(stoppedAt<.95,'excerpt playback stops near the selected boundary');
        await page.locator('#transcription-start').fill('0');await page.locator('#transcription-start').blur();
        await page.locator('#transcription-duration').fill('');await page.locator('#transcription-duration').blur();
        await analyze.click();await finish();
        const source=await page.locator('#transcription-source').inputValue();
        await page.locator('#transcription-source').fill(source.replace('tempo 120','tempo 100'));
        await page.getByRole('button',{name:'Play edited source',exact:true}).click();await finish();
        for(const [name,file] of [['Export SoundScript','edited.ss'],['Export WAV','edited.wav']]) {
            const pending=page.waitForEvent('download');await page.getByRole('button',{name,exact:true}).click();
            const downloaded=await pending;await downloaded.saveAs(path.join(output,file));await finish();
            assert.ok(fs.statSync(path.join(output,file)).size>44);
        }
        assert.match(fs.readFileSync(path.join(output,'edited.ss'),'utf8'),/tempo 100/);
        await page.getByRole('button',{name:'Stop',exact:true}).click();
        await page.locator('#transcription-mode').selectOption('ExtractMelody');assert.equal(await exportSource.isEnabled(),false);
        await page.locator('#transcription-mode').selectOption(browserMode);
        await page.locator('#transcription-file').setInputFiles(noise);await ready();await analyze.click();await finish();
        assert.equal(await exportSource.isEnabled(),false);assert.match(await page.locator('.transcription-workspace [role="status"]').innerText(),/rejected/i);
        await page.locator('#transcription-file').setInputFiles(long);await ready();await analyze.click();
        await page.getByRole('button',{name:'Cancel analysis',exact:true}).click();await finish();
        assert.equal(await exportSource.isEnabled(),false);assert.match(await page.locator('.transcription-workspace [role="status"]').innerText(),/cancelled/i);
        await page.locator('#transcription-file').setInputFiles(triad);await ready();await analyze.click();await finish();
        await page.setViewportSize({width:390,height:844});
        await page.screenshot({path:path.join(output,'polyphonic-mobile.png'),fullPage:true});
        assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
        if(analysisMode==='percussion') {
            await page.locator('#music-workspace-tab').click();
            await page.locator('#midi-editor textarea').fill('tempo 120 track drums { hit kick :0.5 hit snare :0.5 hit hat :0.5 }');
            await page.getByRole('button',{name:'Run',exact:true}).first().click();
            await page.getByText('SoundScript.Wave · no MIDI step',{exact:true}).first().waitFor();
            assert.equal(await page.getByRole('button',{name:'Download WAV',exact:true}).first().isVisible(),true);
        }
        assert.deepEqual(errors,[]);
        console.log(analysisMode + ': PASS CLI/browser score parity, detected events, excerpt playback, editing/replay, exports, rejection, stale-output invalidation, cancellation and mobile layout');
    } finally {await browser.close();server.close();}
})().catch(e=>{console.error(e);server.close();process.exitCode=1;});
