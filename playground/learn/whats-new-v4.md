# What's New in V4

SoundScript **V4** adds **offline timbre synthesis** — the missing third
dimension after pitch and rhythm. Give it MIDI from `compose` (or any script)
plus a SoundCSS stylesheet, and it produces deterministic WAV or OGG audio.

## Headline feature: SoundScript.Timbre

```
MIDI → SoundCSS → SpectralEngine → WAV/OGG
```

- **SoundCSS** (`.ssc`) — declarative phoneme styling (`burst`, `formant1`,
  `noise`, `smoothness`, …)
- **PhonemeTimbreMapper** — built-in timbre table with total fallback
- **MidiToTimbreTimeline** — 8 ms frame grid aligned to MIDI notes
- **SpectralEngine** — formants, noise, plosive bursts, nasal resonance
- **OfflineRenderer** — slow-motion, deterministic, no neural nets

Protected subsystems (Core, Parser, Interpreter, Voice, MIDI, PhonemeComposer)
are **unchanged**. All V4 code lives in the new `SoundScript.Timbre` project.

## CLI: `render`

```bash
soundscript render output.mid --css examples/default.ssc --out speech.wav \
  --text "Twinkle twinkle little star"
```

| Flag | Purpose |
|------|---------|
| `--css` | SoundCSS stylesheet (required) |
| `--out` | `.wav` or `.ogg` output path |
| `--text` | Source text for phoneme alignment (recommended) |

## Playground: Render Audio

The **Render Audio** button next to **Compose from text** runs the full V4
pipeline in the browser: compose → MIDI → offline timbre → WAV playback.
Download **WAV** after rendering.

## Full pipeline (V3.1 + V4)

```
text → syllables → phonemes → gestures → AST → MIDI → TIMBRE → AUDIO
```

## Documentation

| Page | Topic |
|------|-------|
| [v4-architecture.md](v4-architecture.md) | System design |
| [soundcss.md](soundcss.md) | Stylesheet language |
| [timbre-engine.md](timbre-engine.md) | Synthesis modules |
| [text-to-melody.md](text-to-melody.md) | Updated end-to-end pipeline |
| [cli.md](cli.md) | `render` verb |

## Tests

- SoundCSS parsing
- MIDI → timeline alignment
- Spectral engine determinism
- SHA-256 hashes of rendered WAV bytes

## Previous releases

→ [What's new in V3.1](whats-new-v3.1.md) — PhonemeComposer text-to-melody
