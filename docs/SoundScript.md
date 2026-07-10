# SoundScript Documentation

SoundScript is an open-source, deterministic music DSL that compiles text to MIDI (or, as of V7, directly to WAV). V8 adds **vocal stem mixing** into exported Wave files. It runs as a cross-platform .NET CLI (Windows, macOS, Linux) and as a browser playground that works in any modern browser (Chrome, Edge, Firefox, Safari). This is the documentation hub for **V8**.

## Documentation Index

### Start Here

| Document | Description |
|----------|-------------|
| [user-guide.md](user-guide.md) | Hands-on user guide — from first note to industrial audio cues, with runnable examples |

### V8 Features

| Document | Description |
|----------|-------------|
| [whats-new-v8.md](whats-new-v8.md) | V8 changelog — vocal stems in Wave export |
| [wave-grammar.md](wave-grammar.md) | `.ssw` grammar — `sample`, `speak sample=`, plus V7 `effect`/`speak` |

### V7 Features

| Document | Description |
|----------|-------------|
| [whats-new-v7.md](whats-new-v7.md) | V7 changelog — SoundScript.Wave reaches the Playground |

### V3.1 Features

| Document | Description |
|----------|-------------|
| [whats-new-v3.1.md](whats-new-v3.1.md) | V3.1 changelog — PhonemeComposer |
| [text-to-melody.md](text-to-melody.md) | Text-to-melody pipeline — compose from plain text |
| [phoneme-composer.md](phoneme-composer.md) | PhonemeComposer module reference |

### V3 Features

| Document | Description |
|----------|-------------|
| [whats-new-v3.md](whats-new-v3.md) | V3 changelog |
| [phrases-v3.md](phrases-v3.md) | Phrase engine v3 — curves, envelopes, articulation, timing |

### V2 Features

| Document | Description |
|----------|-------------|
| [whats-new-v2.md](whats-new-v2.md) | V2 changelog |
| [imports.md](imports.md) | Multi-file imports |
| [blocks.md](blocks.md) | Named blocks |
| [track-metadata.md](track-metadata.md) | Gain + humanize |
| [tempo-automation.md](tempo-automation.md) | Tempo ramps |
| [layers.md](layers.md) | Instrument layers |
| [humanization.md](humanization.md) | Deterministic jitter |
| [advanced-chords.md](advanced-chords.md) | drop2, inv1, spread |
| [phrases.md](phrases.md) | Phrase engine v2 |
| [patterns.md](patterns.md) | Pattern engine |
| [orchestration.md](orchestration.md) | Orchestration helpers |

### Reference

| Document | Description |
|----------|-------------|
| [language-reference.md](language-reference.md) | Complete syntax |
| [cli.md](cli.md) | CLI reference (`run`, `compose`, `prosody`, `render`, `wave`, `vocal`) |
| [pipeline.md](pipeline.md) | Interpreter pipeline |
| [architecture.md](architecture.md) | System architecture |
| [examples.md](examples.md) | Example catalog |

### v1.2 Foundation

| Document | Description |
|----------|-------------|
| [notation.md](notation.md) | Notation engine (Phase 2) |
| [expressive-notation.md](expressive-notation.md) | Rests, ties, articulations (Phase 3) |
| [stabilization.md](stabilization.md) | Timing and voicing (Phase 1) |
| [musical-intelligence.md](musical-intelligence.md) | Contour and spacing (Phase 4) |
| [playback-quality.md](playback-quality.md) | Playback shaping (Phase 5) |
| [whats-new-v1.2.md](whats-new-v1.2.md) | v1.2 changelog |

## Quick Start

```bash
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/full-v2-showcase.ss
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"
dotnet run --project src/SoundScript.Cli -- wave examples/jingle-bells-vocal.ssw jingle.wav --offline-tts prosody
```

## Playground

**[soundscript.net/playground](https://soundscript.net/playground/)**

Runs fully client-side (Blazor WebAssembly + Web Audio) in any modern browser — Chrome, Edge, Firefox, and Safari, on desktop and mobile. No account, no server, no installation.

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
```

## Design Philosophy

- **Deterministic** — same script, same MIDI
- **Text-first** — music as code
- **Layered engine** — parse, interpret, shape, export
- **Non-destructive warnings** — intelligence adjusts; never aborts
