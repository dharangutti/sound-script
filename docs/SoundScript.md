# SoundScript Documentation (v1.2)

A tiny, deterministic music DSL that turns simple text into professional-sounding MIDI.

**[What's New in v1.2](whats-new-v1.2.md)** · **[Playground](https://soundscript.net/playground/)** · **[GitHub](https://github.com/dharangutti/sound-script)**

---

## Documentation Index

| Document | Description |
|----------|-------------|
| [language-reference.md](language-reference.md) | Complete DSL syntax reference |
| [notation.md](notation.md) | Notation engine (Phase 2) |
| [expressive-notation.md](expressive-notation.md) | Rests, ties, articulations, dynamics (Phase 3) |
| [stabilization.md](stabilization.md) | Beat math, voicing, sync (Phase 1) |
| [musical-intelligence.md](musical-intelligence.md) | Contour, spacing, dynamics (Phase 4) |
| [playback-quality.md](playback-quality.md) | Playback shaping pipeline (Phase 5) |
| [pipeline.md](pipeline.md) | Interpreter and shaping flow |
| [architecture.md](architecture.md) | System architecture |
| [examples.md](examples.md) | Example catalog |
| [whats-new-v1.2.md](whats-new-v1.2.md) | v1.2 changelog |
| [PLAYGROUND.md](PLAYGROUND.md) | Playground build and verification |

---

## Quick Start

```bash
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

## Interpreter Pipeline

```
DSL → Tokenizer → Parser → AST → Interpreter → PlaybackShaper → MIDI
```

Full details: [pipeline.md](pipeline.md)

## Try in Browser

**[https://soundscript.net/playground/](https://soundscript.net/playground/)**

Client-side Blazor WASM — tokenizer through Web Audio playback, fully offline.

### Build Playground

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
```

Output: `docs/playground/` → deployed to GitHub Pages.

---

## Design Philosophy

- **Tiny language** — human-readable, hobby-grade syntax
- **Deterministic** — same input always produces the same MIDI
- **MIDI-first** — no audio synthesis in the engine
- **Additive growth** — all prior syntax remains valid
- **No randomness** — reproducible output every time

---

## License

See [LICENSE](../LICENSE) in the repository root.
