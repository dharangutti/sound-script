// Build a local listening page from the measured MIDI/Wave/browser artifacts.
const fs = require('node:fs');
const path = require('node:path');
const out = path.resolve(process.argv[2] || 'artifacts/performance');
const titles = { harbor: 'Harbor Lights', bells: 'Jingle Bells', ode: 'Ode to Joy', clockwork: 'Clockwork Garden', lanterns: 'Floating Lanterns' };
const metrics = [];
const cards = [];
for (const [name, title] of Object.entries(titles)) {
    const key = `performance-${name}`;
    const before = JSON.parse(fs.readFileSync(path.join(out, `${key}-before.json`)));
    const after = JSON.parse(fs.readFileSync(path.join(out, `${key}-after.json`)));
    const notes = after.midi.flatMap(t => t.Notes);
    const connected = notes.filter(n => n.Performance?.Connected).length;
    const overlaps = [];
    for (const track of after.midi) {
        for (const channel of new Set(track.Notes.map(n => n.Channel))) {
            const voice = track.Notes.filter(n => n.Channel === channel).sort((a, b) => a.StartSeconds - b.StartSeconds);
            for (let i = 1; i < voice.length; i++) if (voice[i].Performance?.Connected)
                overlaps.push((voice[i - 1].StartSeconds + voice[i - 1].DurationSeconds - voice[i].StartSeconds) * 1000);
        }
    }
    metrics.push({ key, notes: notes.length, connected, maxOverlapMs: Math.max(0, ...overlaps),
        legacyMidiNotes: before.midi.reduce((sum, t) => sum + t.Notes.length, 0) });
    const sections = [];
    for (const [suffix, rail] of [['-browser', 'Browser GM samples'], ['', 'SoundScript Wave']]) {
        const files = ['before', 'after'].map(label => `${key}-${label}${suffix}.wav`);
        if (!files.every(file => fs.existsSync(path.join(out, file)))) continue;
        const buffers = files.map(file => fs.readFileSync(path.join(out, file)));
        // Common gain for each pair: preserve before/after level differences, make quiet samples audible.
        const starts = buffers.map(buffer => {
            let i = 12;
            while (buffer.toString('ascii', i, i + 4) !== 'data') i += 8 + buffer.readUInt32LE(i + 4) + (buffer.readUInt32LE(i + 4) % 2);
            return i + 8;
        });
        let peak = 1;
        buffers.forEach((buffer, i) => { for (let p = starts[i]; p < buffer.length - 1; p += 2) peak = Math.max(peak, Math.abs(buffer.readInt16LE(p))); });
        const gain = Math.min(32, 28000 / peak);
        buffers.forEach((buffer, i) => {
            for (let p = starts[i]; p < buffer.length - 1; p += 2) buffer.writeInt16LE(Math.round(buffer.readInt16LE(p) * gain), p);
            fs.writeFileSync(path.join(out, files[i].replace('.wav', '-listen.wav')), buffer);
        });
        sections.push(`<h3>${rail}</h3><div class="pair">${files.map((file, i) => `<label>${i ? 'Expressive' : 'Legacy'}<audio controls preload="none" src="${file.replace('.wav', '-listen.wav')}"></audio></label>`).join('')}</div>`);
    }
    cards.push(`<section><h2>${title}</h2><p>${notes.length} notes · ${connected} connected transitions · maximum note overlap ${Math.max(0, ...overlaps).toFixed(1)} ms</p>${sections.join('')}<p><a href="${key}.ss">Score</a> · <a href="${key}-before.mid">Legacy MIDI</a> · <a href="${key}-after.mid">Expressive MIDI</a></p></section>`);
}
fs.writeFileSync(path.join(out, 'metrics.json'), JSON.stringify(metrics, null, 2));
fs.writeFileSync(path.join(out, 'index.html'), `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>SoundScript performance comparisons</title><style>body{font:17px/1.6 system-ui;margin:40px auto;padding:0 24px;max-width:1050px;color:#17332e;background:#f5f7f3}h1,h2{line-height:1.2}h1{font-size:38px}section{background:white;border:1px solid #d8e1d8;border-radius:16px;padding:24px;margin:24px 0}.pair{display:flex;gap:28px;flex-wrap:wrap}label{display:grid;gap:8px;flex:1}audio{width:100%;min-width:250px}h3{font-size:17px;margin-bottom:8px}a{color:#116950}p{max-width:850px}</style><h1>Hear the performance layer</h1><p>Five identical scores, played with and without <code>perform expressive</code>. Compare connected phrases, repeated-note clarity, rests, long-note dynamics, and staccato pulse. Each before/after pair uses the same listening gain.</p><p>These are real generated recordings. Automated checks verify timing and deterministic rendering; musical acceptance requires listening. The browser uses the repository’s GM samples; Wave uses synthetic instrument-family colors.</p>${cards.join('')}<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(b!==a)b.pause()})))</script></html>`);
console.log(JSON.stringify(metrics, null, 2));
