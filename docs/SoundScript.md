# SoundScript Documentation (V2)

SoundScript is a tiny music DSL that compiles text to MIDI. This is the documentation hub for **V2**.

## Documentation Index

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
```

## Playground

**[soundscript.net/playground](https://soundscript.net/playground/)**

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
```

## Design Philosophy

- **Deterministic** — same script, same MIDI
- **Text-first** — music as code
- **Layered engine** — parse, interpret, shape, export
- **Non-destructive warnings** — intelligence adjusts; never aborts
