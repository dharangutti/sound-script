const fs = require('node:fs'), path = require('node:path'), {execFileSync} = require('node:child_process');
const root = path.resolve(__dirname, '..');
const tracked = execFileSync('git', ['diff', '--name-only', 'main'], {cwd:root, encoding:'utf8'}).trim().split(/\r?\n/);
const untracked = execFileSync('git', ['ls-files', '--others', '--exclude-standard'], {cwd:root, encoding:'utf8'}).trim().split(/\r?\n/);
let count = 0;
const errors = [];
for (const file of new Set([...tracked, ...untracked].filter(f => f.endsWith('.md')))) {
  const source = fs.readFileSync(path.join(root, file), 'utf8').replace(/```[\s\S]*?```|~~~[\s\S]*?~~~/g, '');
  for (const match of source.matchAll(/!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)/g)) {
    const href = match[1];
    if (/^[a-z]+:/i.test(href)) continue;
    const [target, anchor] = href.split('#');
    const resolved = path.resolve(root, path.dirname(file), decodeURIComponent(target || path.basename(file)));
    count++;
    if (!fs.existsSync(resolved)) { errors.push(`${file}: missing ${href}`); continue; }
    if (anchor && resolved.endsWith('.md')) {
      const headings = fs.readFileSync(resolved, 'utf8').split(/\r?\n/).filter(l => /^#+ /.test(l))
        .map(l => l.replace(/^#+ /, '').toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/ /g, '-'));
      if (!headings.includes(decodeURIComponent(anchor))) errors.push(`${file}: missing anchor ${href}`);
    }
  }
}
if (errors.length) { console.error(errors.join('\n')); process.exitCode = 1; }
else console.log(`PASS: ${count} local Markdown links, including heading anchors`);
