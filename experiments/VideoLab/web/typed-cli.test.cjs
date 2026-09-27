// Exercise the CLI boundary, where shell assignments become typed JSON values.
const {execFileSync,spawnSync}=require('node:child_process');
const assert=require('node:assert/strict'),path=require('node:path');
const cwd=path.resolve(__dirname,'..');
const dll=process.env.VIDEOLAB_DLL||'bin/Release/net10.0/VideoLab.dll';
for(const audience of ['shopfloor','qa','engineering']){
    const scene=JSON.parse(execFileSync('dotnet',[dll,'inspect','examples/audience.json','30',`audience=${audience}`,'showSafety=false'],{cwd,encoding:'utf8'}));
    assert.ok(scene.Layers.filter(l=>l.Group&&l.Included).every(l=>l.Group===audience));
    assert.equal(scene.Layers.find(l=>l.Source.startsWith('SAFETY:')).Included,false);
}
for(const assignments of [['audience=other'],['showSafety=1'],['musicGain=qa'],['audience=qa','audience=engineering']]){
    const result=spawnSync('dotnet',[dll,'inspect','examples/audience.json','30',...assignments],{cwd,encoding:'utf8'});
    assert.equal(result.status,1,result.stderr);assert.match(result.stderr,/VideoLab:/);
}
console.log('PASS: CLI enum/boolean/decimal parsing, audience isolation and invalid/duplicate assignments.');
