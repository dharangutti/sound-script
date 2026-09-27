// Verify external-file resolution and batch preflight through the actual CLI.
const {execFileSync,spawnSync}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const cwd=path.resolve(__dirname,'..'),dll=process.env.VIDEOLAB_DLL||'bin/Release/net10.0/VideoLab.dll';
const root=fs.mkdtempSync(path.join(cwd,'artifacts/annotation-cli-'));
const datasets={shopfloor:'shopfloor-instructions',qa:'qa-comments',engineering:'engineering-review'};
const batch=Object.entries(datasets).map(([audience,file])=>({output:path.join(root,audience+'.mp4'),annotations:path.join(cwd,'examples/annotations',file+'.json'),parameters:{audience}}));
const batchFile=path.join(root,'batch.json');fs.writeFileSync(batchFile,JSON.stringify(batch));
execFileSync('dotnet',[dll,'batch','examples/annotations.json',batchFile],{cwd});
for(const row of batch){
    assert.ok(fs.statSync(row.output).size>1000);
    execFileSync('ffmpeg',['-v','error','-xerror','-i',row.output,'-f','null','-']);
}
const before=crypto.createHash('sha256').update(fs.readFileSync(batch[0].output)).digest('hex');
const bad=path.join(root,'invalid.json');fs.writeFileSync(bad,'{"schemaVersion":1,"annotations":[],"execute":"host.run()"}');
batch[1].annotations=bad;fs.writeFileSync(batchFile,JSON.stringify(batch));
const failure=spawnSync('dotnet',[dll,'batch','examples/annotations.json',batchFile],{cwd,encoding:'utf8'});
assert.equal(failure.status,1,failure.stderr);
assert.equal(crypto.createHash('sha256').update(fs.readFileSync(batch[0].output)).digest('hex'),before);
// A syntactically invalid dataset must abort before even the first output renders.
const newOutput=path.join(root,'not-created.mp4');batch[0].output=newOutput;fs.writeFileSync(batchFile,JSON.stringify(batch));
assert.equal(spawnSync('dotnet',[dll,'batch','examples/annotations.json',batchFile],{cwd}).status,1);
assert.equal(fs.existsSync(newOutput),false);
console.log('PASS: CLI renders three external datasets; invalid later dataset prevents all batch writes.');
