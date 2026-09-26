const {test} = require('node:test'), assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const {execFileSync} = require('node:child_process');
const {validate,validateFiles,treeHash,stage,index,hash} = require('./labs.cjs');
const lab = () => ({id:'sample',name:'Sample',description:'Experiment',reason:'Not yet hosted',branch:'experiments/sample',commit:'a'.repeat(40),milestone:'sample-v1',status:'poc',mvp:false,publish:false,artifact:null});
const manifest = () => ({schemaVersion:1,labs:[lab()]});
test('catalog supports zero live Labs and escapes metadata',()=>{const m=manifest();m.labs[0].name='<unsafe>';validate(m);assert.match(index(m),/&lt;unsafe&gt;/);assert.ok(!index(m).includes('href="sample/"'));});
for(const [name,mutate] of Object.entries({duplicate:m=>m.labs.push(lab()),status:m=>m.labs[0].status='ready',branch:m=>m.labs[0].branch='--upload-pack=bad',commit:m=>m.labs[0].commit='HEAD',route:m=>m.labs[0].id='../playground',gate:m=>m.labs[0].publish=true,flag:m=>m.labs[0].publish='false',hiddenArtifact:m=>m.labs[0].artifact={directory:'site'}}))
    test(`rejects ${name}`,()=>{const m=manifest();mutate(m);assert.throws(()=>validate(m));});
test('static resource checks reject escaping, missing and root assets',()=>{
    for(const text of ['<script src="/app.js"></script>','<script src="../app.js"></script>','<link href="missing.css">','<base href="/">','navigator.serviceWorker.register("sw.js")'])
        assert.throws(()=>validateFiles(new Map([['index.html',Buffer.from(text)]])));
    validateFiles(new Map([['index.html',Buffer.from('<a href="../">Labs</a><script src="app.js"></script>')],['app.js',Buffer.from('console.log(1)')]]));
});
test('artifact hashes are deterministic, sensitive to paths and content',()=>{
    const a=new Map([['b',Buffer.from('B')],['a',Buffer.from('A')]]);assert.equal(treeHash(a),treeHash(new Map([...a].reverse())));a.set('a',Buffer.from('changed'));assert.notEqual(treeHash(a),treeHash(new Map([['a',Buffer.from('A')],['b',Buffer.from('B')]])));
});
test('staging rejects unapproved output and preserves production files',()=>{
    const root=fs.mkdtempSync(path.join(os.tmpdir(),'labs-test-'));
    try {fs.writeFileSync(path.join(root,'index.html'),'production');stage(root,manifest());assert.equal(fs.readFileSync(path.join(root,'index.html'),'utf8'),'production');assert.ok(fs.existsSync(path.join(root,'labs/index.html')));assert.ok(!fs.existsSync(path.join(root,'labs/sample')));assert.throws(()=>stage(root,manifest()));}
    finally{fs.rmSync(root,{recursive:true,force:true});}
});
test('pinned Git artifact stages with provenance; tampering fails before writes',()=>{
    const root=fs.mkdtempSync(path.join(os.tmpdir(),'labs-git-'));
    const git=(...args)=>execFileSync('git',['-C',root,...args],{encoding:'utf8',stdio:['ignore','pipe','pipe']}).trim();
    try {
        git('init');git('config','user.name','Fixture');git('config','user.email','fixture@example.invalid');git('config','core.autocrlf','false');
        fs.mkdirSync(path.join(root,'experiments/demo/site'),{recursive:true});
        const bytes=Buffer.from('<h1>Sample</h1>');fs.writeFileSync(path.join(root,'experiments/demo/site/index.html'),bytes);
        const artifactSha256=treeHash(new Map([['index.html',bytes]]));
        const evidence=JSON.stringify({schemaVersion:1,mvp:'pass',artifactSha256,checks:Object.fromEntries(['build','tests','determinism','browser'].map(k=>[k,{result:'pass',command:'fixture test'}]))});
        fs.writeFileSync(path.join(root,'experiments/demo/evidence.json'),evidence);git('add','.');git('commit','-m','Fixture');
        const m=manifest();Object.assign(m.labs[0],{status:'mvp',mvp:true,publish:true,commit:git('rev-parse','HEAD'),artifact:{directory:'experiments/demo/site',sha256:artifactSha256,evidence:'experiments/demo/evidence.json',evidenceSha256:hash(evidence)}});
        const site=path.join(root,'staging');fs.mkdirSync(site);fs.writeFileSync(path.join(site,'CNAME'),'soundscript.net');stage(site,m,{offline:true,repo:root});
        assert.equal(JSON.parse(fs.readFileSync(path.join(site,'labs/sample/publication.json'))).commit,m.labs[0].commit);
        const bad=path.join(root,'bad');fs.mkdirSync(bad);m.labs[0].artifact.sha256='b'.repeat(64);assert.throws(()=>stage(bad,m,{offline:true,repo:root}),/hash mismatch/);assert.ok(!fs.existsSync(path.join(bad,'labs')));
    }finally{fs.rmSync(root,{recursive:true,force:true});}
});
