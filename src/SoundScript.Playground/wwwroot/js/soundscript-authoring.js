// Source-only editor services. Rendering and playback stay in their canonical adapters.
(function () {
    'use strict';
    const keywords = 'let marker style use tempo bpm time track melody block sequence loop phrase pattern play instrument layer gain humanize velocity rest visual wait sync audio animate set at for over bars voice vocal sing speak sample effect curve transition crescendo decrescendo articulation swing push pull import double reinforce brighten up down updown strum rhythm'.split(' ');
    const shapes = 'rectangle roundedRectangle ellipse circle triangle line arrow ring text'.split(' ');
    const properties = 'x y width height size radius rotation opacity'.split(' ');
    const appearance = 'shape fill stroke strokeWidth text fontSize'.split(' ');
    const instruments = 'piano guitar bass violin cello flute trumpet organ synth'.split(' ');
    const cssProps = 'style persona pitch speed timbre vibrato accent breath emotion gender age energy burst noise brightness formant1 formant2 formant3 smoothness nasal openness harmonic1 harmonic2 harmonic3 noise-fricative noise-plosive transient harmonic-rolloff formant-q noise-band smoothing'.split(' ');
    const durations = ['q', 'h', 'e', 'w', 'quarter', 'half', 'eighth', 'whole'];
    const dynamics = ['p', 'mp', 'mf', 'f'];
    const help = {
        let: 'let name = number, quoted string, or time. File-local, immutable; declare before use. Numeric arithmetic: + - * / and parentheses.',
        marker: 'marker name = 2s. Resolves to an absolute time; use after at. Milliseconds (ms) are also accepted.',
        style: 'style "name" { fill "#RRGGBB" stroke "#RRGGBB" strokeWidth 2 }. Visual appearance defaults.',
        use: 'use "styleName" inside a visual. Local appearance declarations override the style, regardless of order.',
        visual: 'visual "name" for 4s [at 2s] { ... }. Without at, starts at the narrative cursor.',
        set: 'set property number-or-expression. x/y, width/height, size/radius, rotation, opacity; constant throughout the visual lifetime.',
        animate: 'animate property from -> to over 2s. Numeric properties interpolate linearly in the existing timeline.',
        fill: 'Quoted #RRGGBB or "none". Text uses fill; line and ring use stroke.',
        stroke: 'Quoted #RRGGBB or "none". Text outlines are unsupported.',
        strokeWidth: '0–128 logical pixels. Appearance value, not animatable.',
        fontSize: '8–256 logical pixels. Only shape text; appearance value, not animatable.',
        shape: 'rectangle, roundedRectangle, ellipse, circle, triangle, line, arrow, ring, text. Shape is not animatable.',
        text: 'shape text requires quoted text, at most 120 supported Latin characters. Bitmap text displays uppercase.',
        tempo: 'tempo positive-integer BPM, or tempo start -> end over N bars.',
        rest: 'rest q (quarter), h, e, w, or rest for positive-number beats.',
        instrument: 'Select an existing instrument name. Unsupported names are reported by the current compiler.',
        play: 'play blockName, sequenceName, or patternName. Reuses existing music primitives.',
        time: 'time numerator/denominator, e.g. time 4/4.',
        opacity: 'Animatable numeric opacity, 0–1.',
        x: 'Animatable horizontal position. Explicit shapes use logical coordinates; legacy named visuals retain their existing coordinate rules.',
        y: 'Animatable vertical position. Explicit shapes use logical coordinates; legacy named visuals retain their existing coordinate rules.'
    };
    const escape = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    function lex(source) {
        const re = /\/\/[^\n]*|\/\*[\s\S]*?(?:\*\/|$)|"[^"\n]*(?:"|$)|[A-Ga-g](?:[#b♭♮♯]+)?-?\d+|[A-Za-z][A-Za-z0-9]*|\d+(?:\.\d+)?|->|[^\s]/g;
        return Array.from(source.matchAll(re), m => ({ text: m[0], start: m.index, end: m.index + m[0].length,
            kind: m[0].startsWith('//') || m[0].startsWith('/*') ? 'comment' : m[0][0] === '"' ? 'string' : 'code' }));
    }
    function highlight(source, language) {
        const css = language === 'soundcss';
        let end = 0, html = '';
        for (const t of lex(source)) {
            html += escape(source.slice(end, t.start)); end = t.end;
            const word = t.text;
            const cls = t.kind === 'comment' ? 'comment' : t.kind === 'string' ? 'string' :
                (css ? cssProps.includes(word.toLowerCase()) : keywords.includes(word.toLowerCase())) ? (css ? 'prop' : 'keyword') : shapes.includes(word) || appearance.includes(word) || properties.includes(word) ? 'prop' :
                /^[A-Ga-g][#b♭♮♯]*-?\d+$/.test(word) || durations.includes(word) || dynamics.includes(word) ? 'note' :
                /^\d/.test(word) || /^(s|ms|sec|seconds)$/.test(word) ? 'number' : instruments.includes(word) ? 'prop' : 'identifier';
            const color = /^"#[\da-f]{6}"$/i.test(word) ? ` style="text-decoration:underline;text-decoration-color:${word.slice(1, -1)};text-decoration-thickness:3px"` : '';
            html += `<span class="tok-${cls}"${color}>${escape(word)}</span>`;
        }
        return html + escape(source.slice(end));
    }
    function format(source) {
        const tokens = lex(source); let depth = 0, offset = 0;
        return source.split('\n').map(line => {
            const start = offset; offset += line.length + 1;
            const inside = tokens.some(t => t.kind !== 'code' && t.start < start && t.end > start);
            const code = tokens.filter(t => t.kind === 'code' && t.start >= start && t.start < offset - 1);
            const leadingClose = code[0]?.text === '}' ? 1 : 0;
            const result = inside ? line : line.trim() ? '    '.repeat(Math.max(0, depth - leadingClose)) + line.trim() : '';
            depth = Math.max(0, depth + code.filter(t => t.text === '{').length - code.filter(t => t.text === '}').length);
            return result;
        }).join('\n');
    }
    function outline(source) {
        const tokens = lex(source).filter(t => t.kind !== 'comment'), result = [], stack = [];
        for (let i = 0; i < tokens.length; i++) {
            const token = tokens[i];
            if (token.text === '{') {
                let j = i - 1;
                while (j >= 0 && !['visual', 'track', 'block', 'sequence', 'phrase', 'pattern', 'voice', 'melody', 'loop', 'style'].includes(tokens[j].text)
                    && !['{', '}'].includes(tokens[j].text)) j--;
                const begin = j >= 0 ? tokens[j] : token;
                stack.push({ start: begin.start, bodyStart: token.end, end: source.length,
                    label: source.slice(begin.start, token.start).trim(), name: tokens[j + 1]?.text, kind: begin.text });
            } else if (token.text === '}' && stack.length) {
                const item = stack.pop(); item.end = token.end; result.push(item);
            }
        }
        return result.sort((a, b) => a.start - b.start);
    }
    function completions(source, cursor) {
        const all = lex(source), at = all.find(t => t.start < cursor && t.end >= cursor);
        if (at?.kind === 'comment' || at?.kind === 'string') return [];
        const prefix = /[A-Za-z][A-Za-z0-9]*$/.exec(source.slice(0, cursor))?.[0] || '';
        const before = source.slice(0, cursor - prefix.length).trimEnd();
        const previous = lex(before).at(-1)?.text;
        const block = outline(source).filter(b => b.start <= cursor && b.end >= cursor).at(-1);
        let values;
        if (previous === 'instrument' || previous === 'layer') values = instruments;
        else if (previous === 'shape') values = shapes;
        else if (previous === 'set' || previous === 'animate') values = properties;
        else if (previous === 'rest' || /^[A-Ga-g][#b]*\d+$/.test(previous || '')) values = durations;
        else if (previous === 'at') values = all.filter((t, i) => all[i - 1]?.text === 'marker').map(t => t.text);
        else if (block?.kind === 'visual') values = ['set', 'animate', 'use', ...appearance];
        else values = [...keywords, ...dynamics, ...durations, ...'C D E F G A B'.split(' ').flatMap(n => [3, 4, 5].map(o => n + o))];
        return [...new Set(values)].filter(v => v.toLowerCase().startsWith(prefix.toLowerCase())).map(value => ({ value, start: cursor - prefix.length, end: cursor }));
    }
    function transpose(source, start, end, semitones) {
        const changes = lex(source).filter(t => t.kind === 'code' && t.start >= start && t.end <= end && /^[A-Ga-g][#b]*-?\d+$/.test(t.text));
        const names = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];
        for (const t of changes.reverse()) {
            const m = /^([A-Ga-g])([#b]*)(-?\d+)$/.exec(t.text);
            const midi = (Number(m[3]) + 1) * 12 + { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }[m[1].toUpperCase()] + [...m[2]].reduce((n, c) => n + (c === '#' ? 1 : -1), 0) + semitones;
            if (midi < 0 || midi > 127) throw new Error('Transpose would leave the MIDI range (0–127).');
            source = source.slice(0, t.start) + names[midi % 12] + (Math.floor(midi / 12) - 1) + source.slice(t.end);
        }
        return source;
    }
    function duplicate(source, cursor) {
        const blocks = outline(source), block = blocks.find(b => b.kind === 'visual' && cursor >= b.start && cursor <= b.end);
        if (!block || !block.name?.startsWith('"')) throw new Error('Place the cursor inside a named visual block.');
        // Sequential visuals affect the narrative cursor. Require an absolute pin
        // before duplicating so existing later intervals never move implicitly.
        if (!lex(source.slice(block.start, block.bodyStart)).some(t => t.text === 'at')) throw new Error('Add an explicit at placement before duplicating this visual.');
        let number = 2, name;
        do { name = block.name.slice(0, -1) + ' copy ' + number++ + '"'; } while (blocks.some(b => b.name === name));
        const text = source.slice(block.start, block.end).replace(block.name, name);
        return source.slice(0, block.end) + '\n\n' + text + source.slice(block.end);
    }
    function attach(textarea, id, reference, refresh, anchor) {
        if (textarea.dataset.authoring) return () => {};
        textarea.dataset.authoring = 'true';
        const panel = document.createElement('div'); panel.className = 'authoring-panel';
        panel.innerHTML = '<div class="authoring-tools"></div><div class="authoring-help" role="status"></div><div class="authoring-completions"></div><details><summary>Document outline / block navigation</summary><div class="authoring-outline"></div></details><details open><summary>Validation &amp; export preflight</summary><div class="authoring-diagnostics" role="status"></div></details>';
        (id === 'visual-timeline-source' ? anchor.parentElement : anchor).insertAdjacentElement('afterend', panel);
        const tools = panel.querySelector('.authoring-tools'), status = panel.querySelector('.authoring-help'), diagnostics = panel.querySelector('.authoring-diagnostics');
        const suggestions = panel.querySelector('.authoring-completions');
        let timer, version = 0, last = null;
        const muted = new Set(), solo = new Set();
        let audition = 0;
        function write(text) {
            textarea.value = text; textarea.dispatchEvent(new Event('input', { bubbles: true })); refresh(); changed();
        }
        function action(label, run) {
            const button = document.createElement('button'); button.type = 'button'; button.className = 'btn btn-secondary'; button.textContent = label;
            button.onclick = async () => { try { await run(); } catch (e) { status.textContent = e.message; } };
            tools.append(button); return button;
        }
        function select(start, end) { textarea.focus(); textarea.setSelectionRange(start, end); textarea.dispatchEvent(new Event('select')); }
        action('Format', () => write(format(textarea.value)));
        action('Compile / Validate', () => validate(++version));
        action('Complete (Ctrl+Space)', complete);
        action('Match block', () => {
            const block = outline(textarea.value).filter(b => b.start <= textarea.selectionStart && b.end >= textarea.selectionStart).at(-1);
            if (block) select(block.start, block.end);
        });
        const search = document.createElement('input'); search.placeholder = 'Find text'; search.setAttribute('aria-label', 'Find text'); tools.append(search);
        const replacement = document.createElement('input'); replacement.placeholder = 'Replace with'; replacement.setAttribute('aria-label', 'Replace with'); tools.append(replacement);
        action('Find next', () => {
            if (!search.value) return;
            let start = textarea.value.indexOf(search.value, textarea.selectionEnd);
            if (start < 0) start = textarea.value.indexOf(search.value);
            if (start >= 0) select(start, start + search.value.length);
            else status.textContent = 'No matches.';
        });
        action('Replace selection', () => {
            const a = textarea.selectionStart, b = textarea.selectionEnd;
            if (search.value && textarea.value.slice(a, b) === search.value) write(textarea.value.slice(0, a) + replacement.value + textarea.value.slice(b));
        });
        action('Rename constant', async () => {
            const source = textarea.value;
            const token = lex(source).find(t => t.kind === 'code' && t.start <= textarea.selectionStart && t.end >= textarea.selectionStart);
            if (!token || !replacement.value) throw new Error('Place the cursor in a constant name and enter its new name in Replace with.');
            const result = await reference.invokeMethodAsync('RenameConstant', source, token.text, replacement.value);
            if (source === textarea.value) write(result);
        });
        action('Transpose +1', () => write(transpose(textarea.value, textarea.selectionStart, textarea.selectionEnd, 1)));
        action('Transpose −1', () => write(transpose(textarea.value, textarea.selectionStart, textarea.selectionEnd, -1)));
        action('Octave +', () => write(transpose(textarea.value, textarea.selectionStart, textarea.selectionEnd, 12)));
        action('Octave −', () => write(transpose(textarea.value, textarea.selectionStart, textarea.selectionEnd, -12)));
        action('Duplicate visual', () => write(duplicate(textarea.value, textarea.selectionStart)));
        const duration = document.createElement('select'); duration.setAttribute('aria-label', 'Duration for selected notes');
        for (const d of durations.slice(0, 4)) duration.add(new Option(d, d)); tools.append(duration);
        action('Set selected note durations', () => {
            const source = textarea.value, tokens = lex(source).filter(t => t.kind !== 'comment'); let result = source;
            for (let i = tokens.length - 1; i > 0; i--) {
                const t = tokens[i], previous = tokens[i - 1];
                if (t.start >= textarea.selectionStart && t.end <= textarea.selectionEnd && durations.includes(t.text)
                    && (previous.text === 'rest' || /^[A-Ga-g][#b]*-?\d+$/.test(previous.text)))
                    result = result.slice(0, t.start) + duration.value + result.slice(t.end);
            }
            write(result);
        });
        if (id === 'midi-editor') {
            const start = document.createElement('input'), end = document.createElement('input');
            for (const [input, label, value] of [[start, 'Audition range start seconds', '0'], [end, 'Audition range end seconds', '4']]) {
                input.type = 'number'; input.min = '0'; input.step = '0.1'; input.value = value; input.setAttribute('aria-label', label); input.title = label; tools.append(input);
            }
            const ruler = document.createElement('div'); ruler.className = 'authoring-music-ruler'; panel.append(ruler);
            action('Audition range', async () => {
                const from = Number(start.value), to = Number(end.value);
                if (!(to > from && from >= 0)) throw new Error('Choose an end time after the start.');
                const source = textarea.value, request = ++audition;
                const result = await reference.invokeMethodAsync('PrepareMusicPreview', source, [...muted], [...solo]);
                if (source !== textarea.value || request !== audition) return;
                const bytes = Uint8Array.from(atob(result.midi), c => c.charCodeAt(0));
                const timing = await window.SoundScriptMidi.startPlayback(bytes, from, to);
                if (!timing) throw new Error('Audio is unavailable.');
                ruler.replaceChildren();
                const beatLabel = document.createElement('p'); ruler.append(beatLabel);
                const marks = document.createElement('div'); marks.className = 'authoring-beat-marks'; ruler.append(marks);
                for (const beat of result.beats.filter(b => b.seconds >= from && b.seconds <= to)) {
                    const mark = document.createElement('span'); mark.textContent = `bar ${Math.floor(beat.beat / result.beatsPerBar) + 1} · ${beat.beat % result.beatsPerBar + 1}`;
                    mark.style.left = `${100 * (beat.seconds - from) / (to - from)}%`; marks.append(mark);
                }
                const tick = () => {
                    if (request !== audition) return;
                    const now = from + window.SoundScriptMidi.currentTime() - timing.clockStart;
                    const beat = result.beats.filter(b => b.seconds <= now).at(-1);
                    beatLabel.textContent = `${Math.max(from, now).toFixed(2)}s · bar ${beat ? Math.floor(beat.beat / result.beatsPerBar) + 1 : 1} · beat ${beat ? beat.beat % result.beatsPerBar + 1 : 1}`;
                    if (now < Math.min(to, result.duration)) requestAnimationFrame(tick);
                    else beatLabel.textContent += ' · complete';
                }; requestAnimationFrame(tick);
            });
            action('Stop audition', () => { audition++; window.SoundScriptMidi.stop(); });
            action('Preview note / instrument', async () => {
                const token = lex(textarea.value).find(t => t.start <= textarea.selectionStart && t.end >= textarea.selectionStart)?.text;
                const instrument = instruments.includes(token) ? token : 'piano';
                const note = /^[A-Ga-g][#b]*-?\d+$/.test(token || '') ? token : 'C4';
                const result = await reference.invokeMethodAsync('PrepareMusicPreview', `instrument ${instrument}\n${note} q`, [], []);
                await window.SoundScriptMidi.startPlayback(Uint8Array.from(atob(result.midi), c => c.charCodeAt(0)));
            });
        }
        const color = document.createElement('input'); color.type = 'color'; color.title = 'Replace the selected fill/stroke color literal'; color.setAttribute('aria-label', color.title);
        color.addEventListener('input', () => {
            const tokens = lex(textarea.value), t = tokens.find(t => t.start <= textarea.selectionStart && t.end >= textarea.selectionEnd && /^"#[\da-f]{6}"$/i.test(t.text));
            const previous = t && tokens[tokens.indexOf(t) - 1]?.text;
            if (!t || !['fill', 'stroke'].includes(previous)) { status.textContent = 'Place the cursor inside a literal fill or stroke color.'; return; }
            write(textarea.value.slice(0, t.start) + '"' + color.value + '"' + textarea.value.slice(t.end)); select(t.start, t.end);
        }); tools.append(color);
        function complete() {
            suggestions.replaceChildren();
            for (const item of completions(textarea.value, textarea.selectionStart)) {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = item.value; button.title = help[item.value] || item.value;
                button.onclick = () => { write(textarea.value.slice(0, item.start) + item.value + textarea.value.slice(item.end)); select(item.start + item.value.length, item.start + item.value.length); suggestions.replaceChildren(); };
                suggestions.append(button);
            }
        }
        async function validate(request) {
            const source = textarea.value;
            diagnostics.textContent = 'Validating…';
            try {
                const result = await reference.invokeMethodAsync('ValidateAuthoring', id, source);
                if (request !== version || source !== textarea.value || !textarea.isConnected) return;
                last = result; diagnostics.replaceChildren();
                const summary = document.createElement('p');
                summary.textContent = `${result.hasErrors ? 'Compilation failed' : 'Compiled'} · ${result.tracks.length} tracks · ${result.visualCount} visuals · audio ${result.audioDuration.toFixed(3)}s · visual ${result.visualDuration.toFixed(3)}s · total ${result.totalDuration.toFixed(3)}s. ${result.audioBasis}.`;
                diagnostics.append(summary);
                if (result.visualCount) { const resolution = document.createElement('p'); resolution.textContent = 'Media: 1280 × 720. FPS uses the Export Clip selector. Warnings do not change timing.'; diagnostics.append(resolution); }
                for (const track of result.tracks) {
                    const row = document.createElement('p'); row.textContent = `${track.name}: ${track.durationSeconds.toFixed(3)}s, ${track.events} events`;
                    if (id === 'midi-editor') for (const [label, set] of [['Mute', muted], ['Solo', solo]]) {
                        const control = document.createElement('label'), input = document.createElement('input'); input.type = 'checkbox'; input.checked = set.has(track.name);
                        input.onchange = () => input.checked ? set.add(track.name) : set.delete(track.name); control.append(input, `${label} ${track.name}`); row.append(control);
                    }
                    diagnostics.append(row);
                }
                for (const d of result.diagnostics) {
                    const row = document.createElement('button'); row.type = 'button'; row.className = 'authoring-' + d.severity;
                    row.textContent = `${d.severity.toUpperCase()}${d.line ? ` ${d.line}:${d.column}` : ''}: ${d.message}`;
                    row.onclick = () => { if (!d.line) return; const lines = source.split('\n'); const start = lines.slice(0, d.line - 1).reduce((sum, l) => sum + l.length + 1, 0) + (d.column || 1) - 1; select(start, start + 1); };
                    diagnostics.append(row);
                }
            } catch (error) { if (request === version) diagnostics.textContent = 'Validation unavailable: ' + error.message; }
        }
        function changed() {
            if (id === 'visual-timeline-source') syncVisual();
            clearTimeout(timer); version++; const request = version;
            timer = setTimeout(() => validate(request), 600);
            const container = panel.querySelector('.authoring-outline'); container.replaceChildren();
            for (const block of outline(textarea.value)) {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = block.label;
                button.onclick = () => select(block.start, block.end); container.append(button);
            }
        }
        textarea.addEventListener('input', changed);
        textarea.addEventListener('keydown', e => {
            if (e.ctrlKey && e.code === 'Space') { e.preventDefault(); complete(); }
            if (e.key === 'Escape') suggestions.replaceChildren();
            if (id === 'visual-timeline-source' && (e.key === 'Tab' || e.key === 'Enter')) {
                e.preventDefault(); const start = textarea.selectionStart, end = textarea.selectionEnd;
                const line = textarea.value.slice(textarea.value.lastIndexOf('\n', start - 1) + 1, start);
                const insert = e.key === 'Tab' ? '    ' : '\n' + (/^\s*/.exec(line)?.[0] || '') + (lex(line).filter(t => t.kind === 'code').at(-1)?.text === '{' ? '    ' : '');
                write(textarea.value.slice(0, start) + insert + textarea.value.slice(end)); select(start + insert.length, start + insert.length);
            }
        });
        function cursorHelp() {
            const t = lex(textarea.value).find(t => t.start <= textarea.selectionStart && t.end >= textarea.selectionStart);
            const description = t && (help[t.text] || (properties.includes(t.text) ? 'Animatable numeric visual property.' : durations.includes(t.text) ? 'Musical duration: q=1, h=2, e=0.5, w=4 beats.' : dynamics.includes(t.text) ? 'Dynamic marking: p, mp, mf, f.' : null));
            textarea.title = description || 'Ctrl+Space for contextual completion. Escape to leave the editor.';
            status.textContent = description || '';
        }
        textarea.addEventListener('click', cursorHelp); textarea.addEventListener('keyup', cursorHelp); textarea.addEventListener('select', cursorHelp);
        changed();
        return changed;
    }
    function syncVisual() {
        const textarea = document.getElementById('visual-timeline-source'), pre = document.getElementById('visual-source-highlight');
        if (!textarea || !pre) return;
        if (pre.dataset.source !== textarea.value) { pre.innerHTML = highlight(textarea.value) + '\n'; pre.dataset.source = textarea.value; }
        pre.scrollTop = textarea.scrollTop; pre.scrollLeft = textarea.scrollLeft;
        textarea.onscroll = () => { pre.scrollTop = textarea.scrollTop; pre.scrollLeft = textarea.scrollLeft; };
    }
    window.SoundScriptAuthoring = { lex, highlight, format, outline, completions, transpose, duplicate, attach, syncVisual };
})();
