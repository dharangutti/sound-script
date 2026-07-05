# SoundScript Playground — Verification Checklist (V6)

Use this checklist after building or deploying the playground.

## V2 Presets

The playground ships with **V2** and **Core** preset groups (see `Playground.razor` / `Playground.razor.cs`).

| Preset | Demonstrates |
|--------|--------------|
| **Showcase** | Patterns, blocks, phrases, layers, orchestration |
| **Blocks** | Named blocks + `play` |
| **Metadata** | `gain`, `humanize` |
| **Tempo** | `tempo 120 → 160 over 4 bars` |
| **Layers** | `layer piano` / `layer cello` |
| **Humanize** | Deterministic timing + velocity jitter |
| **Chords+** | `drop2`, `inv1`, `spread` |
| **Phrases** | `phrase { curve soft ... }` |
| **Patterns** | `pattern arp` + `play arp Cmaj q` |
| **Orchestration** | `double octave`, `reinforce bass`, `brighten top` |

**Note:** Imports require the CLI (`ProgramLoader`); they are not available in the browser playground.

## Text-to-Melody (V3.1)

The playground has a **Text-to-Melody** row above the editor:

| UI element | Behavior |
|------------|----------|
| Example input box | Plain text to compose from (default: `Twinkle twinkle little star`) |
| **Compose from text** button | Runs the deterministic `PhonemeComposer` (text → syllables → phonemes → gestures → MIDI), plays the result, and enables **Download MIDI** |
| Output preview | The status line shows syllable count, note count, and BPM (e.g. `Composed 7 syllable(s) into 24 note(s) at 96 BPM.`) |

The composed MIDI is byte-identical to the CLI output for the same text
(`soundscript compose "<text>"`). → [text-to-melody.md](text-to-melody.md)

The pipeline display shows: Tokenizer → Parser → Interpreter → PhraseShaper → PatternExpander → Orchestration → Layers → Humanize → MIDI (+ Voice + PhonemeComposer + ProsodyComposer).

## Word-Level Prosody (V5)

The same **Text-to-Melody** row has a second compose button, using the same
input box:

| UI element | Behavior |
|------------|----------|
| **Compose with Prosody** button | Runs the deterministic `ProsodyComposer` (text → words → word/phrase pitch → syllables → phonemes for rhythm/timbre only → MIDI), plays the result, and enables **Download MIDI** |
| Output preview | The status line shows syllable count, note count, and BPM, tagged `(word-level prosody)` (e.g. `Composed 7 syllable(s) into 24 note(s) at 96 BPM (word-level prosody).`) |

The composed MIDI is byte-identical to the CLI output for the same text
(`soundscript prosody "<text>"`). Unlike **Compose from text**, pitch follows
word stress and sentence contour instead of one fixed pitch per phoneme
category — for "Twinkle twinkle little star" the melody falls gently from
F4 to A#3 across the phrase instead of arpeggiating per phoneme.
→ [word-prosody.md](word-prosody.md)

The output pane also has a **Render Audio (Prosody)** button (next to
**Render Audio**) that runs the same V4 offline timbre pass over the
`ProsodyComposer` MIDI instead of `PhonemeComposer`'s — same SoundCSS timbre
per phoneme, word/syllable-shaped pitch underneath it. Equivalent to chaining
the CLI verbs:

```bash
soundscript prosody "Twinkle twinkle little star" twinkle.mid
soundscript render twinkle.mid --css examples/default.ssc --out twinkle.wav --text "Twinkle twinkle little star"
```

## `.ss` Export (V6)

Both compose buttons (**Compose from text** and **Compose with Prosody**)
also populate a `.ss` export from the same input, using the new
`SoundScript.Parser.SsPrinter`:

| UI element | Behavior |
|------------|----------|
| **View .ss source** (collapsible panel) | Shows the printed `.ss` text for the most recent compose run, formatted with 4-space indentation and one statement per line |
| **Download .ss** button | Downloads the same text as `soundscript.ss`, via the `SoundScriptText.download` JS interop helper (`js/text-download.js`) |

The downloaded `.ss` file is valid input to the CLI's `run` verb and
byte-identical in resulting MIDI to what the compose button itself produced,
as long as it's run unedited:

```bash
soundscript run soundscript.ss soundscript-viass.mid
```

→ [whats-new-v6.md](whats-new-v6.md)

## Render Audio (V4)

| UI element | Behavior |
|------------|----------|
| **Render Audio** button | Composes text to MIDI, then runs offline `OfflineRenderer` (MIDI → SoundCSS → WAV) and plays the WAV via Web Audio |
| **Download WAV** | Appears after a successful render; deterministic output for the same text |

Rendering is **offline only** (slow-motion synthesis in WASM), not real-time timbre streaming.

**V4.1** reconstructs 3–10 pitch cycles per 8 ms frame for richer timbre —
the same text produces different (improved) WAV bytes compared to V4.0.

→ [timbre-engine.md](timbre-engine.md) · [soundcss.md](soundcss.md) · [v4.1-cycle-synthesis.md](v4.1-cycle-synthesis.md) · [v4.1.1-timbre-tuning.md](v4.1.1-timbre-tuning.md)

## Build

- [ ] `dotnet build` succeeds with no errors
- [ ] `dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release` succeeds
- [ ] `docs/playground/index.html` exists
- [ ] `docs/playground/_framework/blazor.webassembly.js` exists
- [ ] `docs/playground/soundfont/samples/C.wav` exists (all 12 pitch classes)

## Local smoke test

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
cd docs/playground
python3 -m http.server 8080
```

- [ ] Open `http://localhost:8080/playground/` — note: for local static serve, use a server that supports the `/playground/` path, or run `dotnet run --project src/SoundScript.Playground` and open `http://localhost:5180/playground/`
- [ ] Page loads without console errors
- [ ] Default script appears in the editor
- [ ] **Run** compiles and plays audio
- [ ] **Stop** halts playback
- [ ] **Download MIDI** saves a `.mid` file
- [ ] Invalid syntax shows an error in the error panel
- [ ] V2 preset buttons (Showcase, Blocks, Metadata, Tempo, Layers, Humanize, Chords+, Phrases, Patterns, Orchestration) load scripts
- [ ] Core preset buttons (Melody, Articulations, Dynamics, Chords, Intelligence, Multi-track, Playback) load scripts
- [ ] **Compose from text** with the default text plays and reports `Composed 7 syllable(s) into 24 note(s) at 96 BPM.`
- [ ] **Download MIDI** after composing saves a file byte-identical to `soundscript compose "Twinkle twinkle little star"`
- [ ] **Compose with Prosody** with the default text plays and reports `Composed 7 syllable(s) into 24 note(s) at 96 BPM (word-level prosody).`
- [ ] **Download MIDI** after composing with prosody saves a file byte-identical to `soundscript prosody "Twinkle twinkle little star"`
- [ ] **Render Audio (Prosody)** with the default text plays and reports `Rendered 7 syllable(s) offline to ...s of audio at 96 BPM (SoundCSS timbre, word-level prosody).`
- [ ] Composing/rendering (any button) with an empty input shows `Nothing to compose: the text is empty.` / `Nothing to render: the text is empty.`
- [ ] Pipeline display shows PhraseShaper, PatternExpander, Orchestration, Layers, Humanize, PhonemeComposer, ProsodyComposer

## Offline test

- [ ] Load the playground once, then disable network in DevTools
- [ ] **Run** still compiles and plays (WASM + local soundfont cached)

## GitHub Pages

- [ ] Push to `main` triggers `.github/workflows/deploy-pages.yml`
- [ ] `gh-pages` branch contains `index.html` (homepage) and `playground/index.html`
- [ ] `https://soundscript.net/` shows homepage with **Playground** button
- [ ] `https://soundscript.net/playground/` loads the Blazor app
- [ ] Custom domain CNAME (`docs/CNAME`) is present on deployed site

## Pages settings (one-time)

1. **Settings → Pages → Source**: Deploy from branch `gh-pages` / `(root)`
2. **Custom domain**: `soundscript.net`

## Mobile audio (iOS Safari, Android Chrome, Samsung Browser)

- [ ] **Run** is the first interaction that creates `AudioContext` (no audio init on page load)
- [ ] First **Run** tap plays audio on mobile (silent mode off)
- [ ] Subsequent **Run** taps reuse context and play normally
- [ ] If blocked, console shows: `AudioContext blocked — ensure silent mode is off.`

Audio unlock flow: compile synchronously → `startPlayback` is the first `await` → `audioContext.resume()` inside the tap handler.

## No external dependencies

- [ ] Network tab shows no CDN or API requests after initial load
- [ ] Soundfont loads from `/playground/soundfont/samples/*.wav` only
