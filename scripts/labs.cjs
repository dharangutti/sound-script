// Main owns publication policy. Experimental branches contribute data/static files,
// never commands executed with the production deployment token.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const repo = path.resolve(__dirname, '..');
const check = (ok, message) => { if (!ok) throw new Error(`Labs: ${message}`); };
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const git = (root, args, binary = false) => execFileSync('git', ['-C', root, ...args], { encoding: binary ? undefined : 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
const safePath = p => typeof p === 'string' && /^[A-Za-z0-9_.\/-]+$/.test(p) && p.split('/').every(s => s && s !== '.' && s !== '..' && !s.startsWith('.'));
const ref = s => typeof s === 'string' && /^[A-Za-z0-9][A-Za-z0-9._/-]*$/.test(s) && !s.includes('..') && !s.includes('//') && !s.endsWith('/') && !s.endsWith('.lock') && s.split('/').every(x => !x.startsWith('.') && !x.endsWith('.'));
const sha = s => typeof s === 'string' && /^[a-f0-9]{40}$/.test(s);
const digest = s => typeof s === 'string' && /^[a-f0-9]{64}$/.test(s);
function validate(m) {
    check(m.schemaVersion === 1 && Array.isArray(m.labs), 'unsupported manifest');
    const ids = new Set();
    for (const lab of m.labs) {
        check(typeof lab.id === 'string' && /^[a-z][a-z0-9-]*$/.test(lab.id) && !ids.has(lab.id), 'invalid or duplicate route'); ids.add(lab.id);
        check(['exploration', 'poc', 'mvp', 'preserved', 'archived'].includes(lab.status), `${lab.id}: invalid status`);
        check(ref(lab.branch) && sha(lab.commit) && ref(lab.milestone), `${lab.id}: invalid branch, commit or milestone`);
        check(typeof lab.publish === 'boolean' && typeof lab.mvp === 'boolean', `${lab.id}: explicit publication and MVP flags required`);
        for (const field of ['name', 'description', 'reason']) check(typeof lab[field] === 'string' && lab[field].length > 0, `${lab.id}: missing ${field}`);
        if (!lab.publish) { check(lab.artifact === null, `${lab.id}: unpublished Lab cannot stage an artifact`); continue; }
        check(lab.mvp && lab.status === 'mvp', `${lab.id}: publication requires MVP`);
        const a = lab.artifact;
        check(a && safePath(a.directory) && a.directory.startsWith('experiments/') && digest(a.sha256), `${lab.id}: invalid artifact contract`);
        check(safePath(a.evidence) && digest(a.evidenceSha256), `${lab.id}: hashed evidence required`);
    }
    return m;
}
function treeHash(files) {
    return hash([...files].sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([name, bytes]) => `${name}\0${hash(bytes)}\n`).join(''));
}
function validateFiles(files) {
    check(files.has('index.html'), 'missing index.html');
    let size = 0;
    for (const [name, bytes] of files) {
        check(safePath(name) && /\.(html|css|js|json|png|jpg|svg|webp|mp4|webm|wav|woff2|wasm)$/.test(name), `unsupported artifact path ${name}`);
        size += bytes.length;
        if (/\.(html|css|js)$/.test(name)) {
            const text = bytes.toString('utf8');
            check(!/serviceWorker|<base\b/i.test(text), `${name}: base rewriting and service workers are unsupported`);
            // Root-relative and external resources escape the reviewed artifact. External
            // hyperlinks and the explicit parent Labs link remain ordinary navigation.
            const refs = [...text.matchAll(/(?:src|href)\s*=\s*["']([^"']+)["']|url\(\s*["']?([^\s)'";]+)|fetch\(\s*["']([^"']+)["']/g)];
            for (const match of refs) {
                const value = match[1] || match[2] || match[3];
                const href = match[0].startsWith('href');
                if (href && (/^https:\/\//.test(value) || value === '../' || value.startsWith('#'))) continue;
                check(!/^(\/|[a-z]+:)/i.test(value), `${name}: resource escapes lab subpath: ${value}`);
                const target = path.posix.normalize(path.posix.join(path.posix.dirname(name), value.split(/[?#]/)[0]));
                check(!target.startsWith('../') && (files.has(target) || target === 'publication.json'), `${name}: missing/escaping resource ${value}`);
            }
        }
    }
    check(size <= 64 * 1024 * 1024 && files.size <= 1000, 'artifact exceeds static publication budget');
}
function readArtifact(root, lab) {
    const prefix = lab.artifact.directory + '/';
    const entries = git(root, ['ls-tree', '-rz', lab.commit, '--', lab.artifact.directory]).split('\0').filter(Boolean);
    const files = new Map();
    for (const entry of entries) {
        const match = /^(\d+) blob ([a-f0-9]+)\t(.+)$/.exec(entry);
        check(match && match[1] === '100644' && match[3].startsWith(prefix), 'only ordinary static files may be published');
        const name = match[3].slice(prefix.length);
        check(safePath(name), 'unsafe Git artifact name');
        files.set(name, git(root, ['cat-file', 'blob', match[2]], true));
    }
    validateFiles(files);
    check(treeHash(files) === lab.artifact.sha256, `${lab.id}: artifact hash mismatch`);
    const evidence = git(root, ['show', `${lab.commit}:${lab.artifact.evidence}`], true);
    check(hash(evidence) === lab.artifact.evidenceSha256, `${lab.id}: evidence hash mismatch`);
    const e = JSON.parse(evidence);
    check(e.schemaVersion === 1 && e.mvp === 'pass' && e.artifactSha256 === lab.artifact.sha256 &&
        ['build', 'tests', 'determinism', 'browser'].every(k => e.checks?.[k]?.result === 'pass' && typeof e.checks[k].command === 'string' && e.checks[k].command.length > 0), `${lab.id}: incomplete MVP evidence`);
    return files;
}
function source(root, lab, offline) {
    if (offline) return root; // Local review only; production always verifies fresh remote refs.
    const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-lab-'));
    git(temp, ['init', '--quiet']);
    // Fetch ancestry, but hydrate only the approved static blobs. The repository's
    // production media/history must not be downloaded again for every Lab.
    git(temp, ['remote', 'add', 'origin', 'https://github.com/dharangutti/sound-script.git']);
    git(temp, ['config', 'remote.origin.promisor', 'true']);
    git(temp, ['config', 'remote.origin.partialclonefilter', 'blob:none']);
    git(temp, ['fetch', '--quiet', '--no-tags', '--filter=blob:none', 'origin',
        `refs/heads/${lab.branch}:refs/heads/lab`, `refs/tags/${lab.milestone}:refs/tags/milestone`]);
    check(git(temp, ['rev-parse', 'refs/tags/milestone^{commit}']).trim() === lab.commit, `${lab.id}: milestone does not pin commit`);
    git(temp, ['merge-base', '--is-ancestor', lab.commit, 'refs/heads/lab']);
    return temp;
}
const escape = s => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function index(m) {
    return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SoundScript Labs</title><style>body{font:17px/1.65 system-ui,sans-serif;background:#0b0d12;color:#e8ecf4;margin:0}main{max-width:960px;margin:auto;padding:36px 24px}a{color:#6ee7b7}h1{font-size:3rem;line-height:1.1}article{background:#141820;border:1px solid #2a3347;border-radius:12px;padding:24px;margin:24px 0}h2{margin:0}dt{color:#9aa6bd}dd{margin:0 0 12px;overflow-wrap:anywhere}code{font-size:.85em}:focus-visible{outline:3px solid #818cf8}</style></head><body><main><nav><a href="../">← SoundScript</a></nav><h1>SoundScript Labs</h1><p>Experimental work exploring possible future SoundScript capabilities. Labs are not stable production APIs and may change or be discontinued.</p><p>Discovery, MVP qualification and public hosting are separate decisions.</p>${m.labs.map(l => `<article id="${l.id}"><h2>${escape(l.name)}</h2><p>${escape(l.description)}</p><dl><dt>Status</dt><dd>${l.publish ? 'Published Lab · MVP' : escape(l.status.toUpperCase())}</dd><dt>Branch</dt><dd><a href="https://github.com/dharangutti/sound-script/tree/${encodeURIComponent(l.branch)}">${escape(l.branch)}</a></dd><dt>Preserved milestone</dt><dd>${escape(l.milestone)}<br><code>${l.commit}</code></dd><dt>Live availability</dt><dd>${escape(l.reason)}</dd></dl>${l.publish ? `<a href="${l.id}/">Open ${escape(l.name)}</a>` : '<p>Not publicly runnable.</p>'}</article>`).join('')}<p><a href="../doc.html?p=labs-hosting.md">Lab lifecycle and publication policy</a></p></main></body></html>`;
}
function inventory(root) {
    const files = new Map();
    function visit(dir, prefix = '') {
        for (const item of fs.readdirSync(dir, {withFileTypes: true})) {
            check(!item.isSymbolicLink(), 'site links are forbidden');
            const name = prefix + item.name, file = path.join(dir, item.name);
            if (item.isDirectory()) visit(file, name + '/'); else files.set(name, fs.readFileSync(file));
        }
    }
    visit(root); return files;
}
function stage(site, m, options = {}) {
    validate(m); check(fs.existsSync(site) && !fs.lstatSync(site).isSymbolicLink(), 'site must be a real directory');
    check(!fs.existsSync(path.join(site, 'labs')), 'fresh site required; labs route already exists');
    const before = treeHash(inventory(site)), approved = [];
    // Validate all inputs before writing, so a failed promotion cannot partially stage Labs.
    for (const lab of m.labs.filter(l => l.publish)) {
        const root = source(options.repo || repo, lab, options.offline);
        try { approved.push([lab, readArtifact(root, lab)]); }
        finally { if (!options.offline) fs.rmSync(root, {recursive: true, force: true}); }
    }
    const target = path.join(site, 'labs'); fs.mkdirSync(target);
    fs.writeFileSync(path.join(target, 'index.html'), index(m));
    for (const [lab, files] of approved) {
        for (const [name, bytes] of files) {
            const file = path.join(target, lab.id, name); fs.mkdirSync(path.dirname(file), {recursive: true}); fs.writeFileSync(file, bytes);
        }
        fs.writeFileSync(path.join(target, lab.id, 'publication.json'), JSON.stringify({id: lab.id, branch: lab.branch, commit: lab.commit, milestone: lab.milestone, artifactSha256: lab.artifact.sha256}, null, 2));
    }
    const after = inventory(site); for (const name of [...after.keys()]) if (name.startsWith('labs/')) after.delete(name);
    check(before === treeHash(after), 'unrelated production content changed');
    console.log(`Labs: ${m.labs.length} listed, ${approved.length} published; production files unchanged.`);
}
if (require.main === module) {
    try {
        const m = validate(JSON.parse(fs.readFileSync(path.join(repo, 'docs/labs-manifest.json'), 'utf8')));
        if (process.argv[2] === 'stage') stage(path.resolve(process.argv[3]), m, {offline: process.argv.includes('--offline')});
        else check(process.argv[2] === 'check', 'usage: labs.cjs check | stage <fresh-site> [--offline]');
    } catch (e) { console.error(e.message); process.exitCode = 1; }
}
module.exports = {validate, validateFiles, treeHash, readArtifact, inventory, stage, index, hash};
