# SoundScript Playground — Verification Checklist (V2)

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

The pipeline display shows: Tokenizer → Parser → Interpreter → PhraseShaper → PatternExpander → Orchestration → Layers → Humanize → MIDI.

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
- [ ] Pipeline display shows PhraseShaper, PatternExpander, Orchestration, Layers, Humanize

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
