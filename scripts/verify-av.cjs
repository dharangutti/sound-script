// Real browser and CLI export verification. See docs/av-stress-test.md.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { execFileSync } = require('node:child_process');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const directory = path.resolve(process.env.SOUNDSCRIPT_AV_ARTIFACTS || path.join(root, 'obj/av-verification'));
const allKeys = fs.readdirSync(directory).filter(f => f.endsWith('.json') && !f.endsWith('.metrics.json') && !f.endsWith('.results.json')).map(f => f.slice(0,-5));
const only = process.argv.find(a=>a.startsWith('--only='))?.slice(7).split(',');
const keys = only ? allKeys.filter(k=>only.includes(k)) : allKeys;
const results = [];
function saveResults(kind) {
  const file=path.join(directory,kind+'.results.json');
  const previous=only && fs.existsSync(file)?JSON.parse(fs.readFileSync(file,'utf8')).filter(r=>!keys.includes(r.key)):[];
  fs.writeFileSync(file,JSON.stringify([...previous,...results].sort((a,b)=>a.key.localeCompare(b.key)),null,2));
}
function validate(file, kind, key) {
  const info = JSON.parse(execFileSync('ffprobe', ['-v','error','-count_frames','-show_streams','-show_format','-of','json',file], {encoding:'utf8'}));
  assert(info.streams.some(s => s.codec_type === 'video' && ['vp8','vp9'].includes(s.codec_name)));
  assert(info.streams.some(s => s.codec_type === 'audio' && s.codec_name === 'opus'));
  // MediaRecorder files may omit a duration; packet timestamps remain inspectable.
  const packets = JSON.parse(execFileSync('ffprobe', ['-v','error','-show_packets','-show_entries','packet=codec_type,pts_time,duration_time','-of','json',file], {encoding:'utf8',maxBuffer:8e6})).packets;
  const ranges = {};
  for (const type of ['audio','video']) {
    const rail = packets.filter(p=>p.codec_type===type);
    ranges[type] = {start:Math.min(...rail.map(p=>Number(p.pts_time))),end:Math.max(...rail.map(p=>Number(p.pts_time)+Number(p.duration_time||0)))};
  }
  assert(Math.abs(ranges.video.end-ranges.audio.end)<0.25, JSON.stringify(ranges));
  assert(Math.abs((ranges.video.end-ranges.video.start)-6)<0.25, JSON.stringify(ranges));
  assert(Math.abs(ranges.video.start-ranges.audio.start)<0.25, JSON.stringify(ranges));
  execFileSync('ffmpeg',['-hide_banner','-v','error','-i',file,'-map','0:v:0','-map','0:a:0','-f','null','-'],{stdio:'pipe'});
  const result={key,kind,bytes:fs.statSync(file).size,frames:Number(info.streams.find(s=>s.codec_type==='video').nb_read_frames),ranges};
  results.push(result); console.log(JSON.stringify(result));
}
(async()=>{
  if (process.argv.includes('--cli')) {
    for(const key of keys) {
      const file=path.join(directory,key+'.cli.webm');
      execFileSync('dotnet',[path.join(root,'src/SoundScript.Cli/bin/Debug/net8.0/soundscript.dll'),'video',path.join(root,`examples/visual-${key}.ssv`),'--output',file,'--fps','24','--width','640','--height','360'],{stdio:'pipe',timeout:180000});
      validate(file,'cli',key);
    }
    saveResults('cli'); return;
  }
  const { chromium }=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
  const server=http.createServer((req,res)=>{
    let url=new URL(req.url,'http://localhost');
    if(url.pathname==='/') {
      res.setHeader('Content-Type','text/html');
      res.end('<!doctype html><canvas width="1280" height="720"></canvas><script src="/js/visual-scene-renderer.js"></script><script src="/js/audio-renderer.js"></script><script src="/js/visual-video-exporter.js"></script>');return;
    }
    const base=url.pathname.startsWith('/js/')?path.join(root,'src/SoundScript.Playground/wwwroot/js'):directory;
    const file=path.join(base,path.basename(url.pathname));
    if(!fs.existsSync(file)){res.writeHead(404);res.end();return;}
    res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.json')?'application/json':file.endsWith('.png')?'image/png':'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const browser=await chromium.launch({headless:true,executablePath:process.env.CHROMIUM_PATH || undefined,args:['--autoplay-policy=no-user-gesture-required']});
  try {
    const page=await browser.newPage({viewport:{width:1280,height:760},acceptDownloads:true});
    const errors=[]; page.on('pageerror',e=>errors.push(e.message));
    for(const key of keys) {
      await page.goto(`http://127.0.0.1:${server.address().port}`);
      const measured=await page.evaluate(async key=>{
        window.plan=await (await fetch('/'+key+'.json')).json();
        window.wav=new Uint8Array(await (await fetch('/'+key+'.wav')).arrayBuffer());
        const context=document.querySelector('canvas').getContext('2d');
        const begin=performance.now();
        for(let i=0;i<30;i++) SoundScriptVisualRenderer.renderScene(context,plan.samples[96]);
        const renderMs=(performance.now()-begin)/30;
        const resumed=await SoundScriptAudio.playWavBytesFromOffset(wav,2);
        SoundScriptAudio.stop();
        if(Math.abs(resumed.durationSeconds-4)>0.001)throw Error('Incorrect seek duration');
        return {renderMs,resumeRemaining:resumed.durationSeconds};
      },key);
      await page.locator('canvas').screenshot({path:path.join(directory,key+'.browser.png')});
      const pending=page.waitForEvent('download');
      await page.evaluate(key=>SoundScriptVideoExporter.exportWebm(plan,wav,key+'.webm'),key);
      const download=await pending;
      const file=path.join(directory,key+'.browser.webm'); await download.saveAs(file);
      validate(file,'browser',key); Object.assign(results.at(-1),measured);
      assert.deepEqual(errors,[]);
    }
    saveResults('browser');
    await page.setContent('<body style="margin:0;background:#eee;display:grid;grid-template-columns:repeat(4,480px);gap:12px">'+allKeys.map(k=>`<figure style="margin:0"><img width="480" src="http://127.0.0.1:${server.address().port}/${k}.browser.png"><figcaption>${k}</figcaption></figure>`).join(''));
    await page.waitForFunction(()=>[...document.images].every(i=>i.complete));
    await page.setViewportSize({width:1960,height:1600});
    await page.screenshot({path:path.join(directory,'contact-sheet.png'),fullPage:true});
  } finally {await browser.close();server.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
