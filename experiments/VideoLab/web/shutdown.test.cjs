// Regression: disconnected/partially sent responses must not fault graceful stop.
const {spawn}=require('node:child_process');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict');
const cwd=path.resolve(__dirname,'..'),dll=process.env.VIDEOLAB_DLL||'bin/Release/net10.0/VideoLab.dll';
const origin='http://127.0.0.1:18746';
(async()=>{
    const before=new Set(fs.readdirSync(os.tmpdir()).filter(n=>n.startsWith('videolab-session-')));
    for(let attempt=0;attempt<3;attempt++){
        const child=spawn('dotnet',[dll,'serve','18746'],{cwd,stdio:['ignore','pipe','pipe']});let log='';
        child.stdout.on('data',d=>log+=d);child.stderr.on('data',d=>log+=d);
        const exited=new Promise(resolve=>child.on('exit',resolve));
        try{
            let token;for(let i=0;i<100;i++){try{token=(await (await fetch(origin+'/api/session')).json()).token;break;}catch{await new Promise(r=>setTimeout(r,100));}}
            assert.ok(token,log);
            const streams=await Promise.all(Array.from({length:4},()=>fetch(origin+'/labs/videolab/proof.json')));
            await Promise.all(streams.map(r=>r.body.cancel()));
            const response=await fetch(origin+'/api/stop',{method:'POST',headers:{Origin:origin,'X-VideoLab-Session':token,'Content-Type':'application/json'},body:'{}'});
            assert.equal(response.status,200);await response.text();assert.equal(await exited,0,log);
        }finally{if(child.exitCode===null){child.kill();await exited;}}
    }
    assert.deepEqual(fs.readdirSync(os.tmpdir()).filter(n=>n.startsWith('videolab-session-')&&!before.has(n)),[]);
    console.log('PASS: three graceful stops with disconnected in-flight responses; no session files remain.');
})().catch(e=>{console.error(e);process.exitCode=1;});
