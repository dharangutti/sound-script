// Regress the data-sequence MP4 mismatch discovered during UI validation.
// Raw filter frames were stable; x264's CPU-dependent decisions were not.
const {execFileSync} = require('node:child_process');
const {createHash} = require('node:crypto');
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const lab=path.resolve(__dirname,'..');let hash;
for(let i=0;i<12;i++) {
    const output='artifacts/encoder-regression-'+i+'.mp4';
    execFileSync('dotnet',[process.env.VIDEOLAB_DLL || 'bin/Release/net10.0/VideoLab.dll','render','examples/data-sequence.json',output],{cwd:lab});
    const current=createHash('sha256').update(fs.readFileSync(path.join(lab,output))).digest('hex');
    if(hash)assert.equal(current,hash,'Data-sequence MP4 changed between identical native renders');hash=current;
}
console.log('PASS: 12 independent data-sequence MP4 renders are byte-identical: '+hash);
