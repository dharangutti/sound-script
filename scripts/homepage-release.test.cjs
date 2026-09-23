const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const repo = path.resolve(__dirname, '..');
const start = '<!--RELEASE_HISTORY_GENERATED_START-->';
const end = '<!--RELEASE_HISTORY_GENERATED_END-->';
const template = `before\n${start}\nplaceholder\n${end}\n<li>V7 static</li>\nafter\n`;
const notes = `# Releases
## 14.0.0 — Current & <safe>
- Add \`Compile<T>()\` and **bold** with *emphasis*.
  Wrapped continuation.
- See [contracts](docs/programmatic-media-runtime.md) and [example](examples/demo.ss).

Candidate-only prose must not appear.
## V13 — Earlier
- First summary
  spans lines.
  - Nested item must not appear.
- Second summary must not appear.
## V8 — Older
- Eight.
## V7 — Static
- Seven must not appear.
`;

function fixture(t, { markdown = notes, html = template, version = '14.0.0', label = 'V14' } = {}) {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-homepage-'));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const index = path.join(dir, 'index.html');
    const releaseNotes = path.join(dir, 'RELEASE_NOTES.md');
    const props = path.join(dir, 'Directory.Build.props');
    fs.writeFileSync(index, html);
    fs.writeFileSync(releaseNotes, markdown);
    fs.writeFileSync(props, `<Project><PropertyGroup><Version>${version}</Version><SoundScriptVersionLabel>${label}</SoundScriptVersionLabel></PropertyGroup></Project>`);
    return {
        read: () => fs.readFileSync(index, 'utf8'),
        run: () => spawnSync('pwsh', ['-NoProfile', '-File', path.join(__dirname, 'update-homepage-release.ps1'),
            '-IndexPath', index, '-ReleaseNotesPath', releaseNotes, '-PropsPath', props], { encoding: 'utf8', cwd: dir })
    };
}

function succeeds(result) {
    assert.equal(result.status, 0, result.stdout + result.stderr);
}

test('renders current bullets and compact older releases with safe markup and working site links', t => {
    const f = fixture(t);
    succeeds(f.run());
    const html = f.read();
    assert.match(html, /Current &amp; &lt;safe&gt;/);
    assert.match(html, /<code>Compile&lt;T&gt;\(\)<\/code> and <strong>bold<\/strong> with <em>emphasis<\/em>\. Wrapped continuation\./);
    assert.match(html, /href="doc.html\?p=programmatic-media-runtime.md"/);
    assert.match(html, /href="https:\/\/github.com\/dharangutti\/sound-script\/blob\/main\/examples\/demo.ss"/);
    assert.match(html, /First summary spans lines\./);
    assert.doesNotMatch(html, /must not appear/);
    assert.equal((html.match(/class="release-current"/g) || []).length, 1);
    assert.equal((html.match(/class="release-previous"/g) || []).length, 2);
    assert.equal(html.split(start)[0], template.split(start)[0]);
    assert.equal(html.split(end)[1], template.split(end)[1]);
    succeeds(f.run());
    assert.equal(f.read(), html, 'generation must be idempotent');
});

test('accepts legacy major-only headings', t => {
    const f = fixture(t, { markdown: notes.replace('14.0.0 —', 'V14 —'), version: '14.0.2' });
    succeeds(f.run());
});

for (const version of ['15.0.0', '14.0.1', '14.0.0-rc.1']) {
    test(`fails version mismatch (${version}) before changing the staged page`, t => {
        const f = fixture(t, { version, label: `V${version.split('.')[0]}` });
        const result = f.run();
        assert.notEqual(result.status, 0);
        assert.match(result.stderr, /does not match/);
        assert.equal(f.read(), template);
    });
}

test('rejects missing, duplicate, or reversed markers without rewriting the page', t => {
    for (const html of ['no markers', template + start, template + end, `${end}\n${start}`]) {
        const f = fixture(t, { html });
        const result = f.run();
        assert.notEqual(result.status, 0);
        assert.match(result.stderr, /ordered pair/);
        assert.equal(f.read(), html);
    }
});

test('fails missing release summaries and unsafe link schemes without rewriting the page', t => {
    for (const markdown of ['## 14.0.0 — Empty\nNo bullets.\n', '## 14.0.0 — Unsafe\n- [click](javascript:alert)\n']) {
        const f = fixture(t, { markdown });
        assert.notEqual(f.run().status, 0);
        assert.equal(f.read(), template);
    }
});

test('real release notes generate every V8+ entry and preserve all static history', t => {
    const html = fs.readFileSync(path.join(repo, 'docs/index.html'), 'utf8');
    const markdown = fs.readFileSync(path.join(repo, 'RELEASE_NOTES.md'), 'utf8');
    const props = fs.readFileSync(path.join(repo, 'Directory.Build.props'), 'utf8');
    const version = props.match(/<Version>([^<]+)<\/Version>/)[1];
    const label = props.match(/<SoundScriptVersionLabel>([^<]+)<\/SoundScriptVersionLabel>/)[1];
    const expected = [...markdown.matchAll(/^## [Vv]?(\d+(?:\.\d+){0,2})\s+[—–-]/gm)]
        .map(m => m[1]).filter(v => Number.parseInt(v, 10) >= 8).map(v => `V${v}`);
    expected[0] = label;
    const f = fixture(t, { html, markdown, version, label });
    succeeds(f.run());
    const output = f.read();
    assert.deepEqual([...output.matchAll(/class="release-(?:current|previous)"><span[^>]+>([^<]+)/g)].map(m => m[1]),
        expected);
    assert.equal(output.split(start)[0], html.split(start)[0]);
    assert.equal(output.split(end)[1], html.split(end)[1]);
});
