# SoundScript V2

A tiny, deterministic music DSL that turns simple text into professional-sounding MIDI.  
Built in C# for curiosity, creativity, and play.

```
import "lib.ss"

block intro {
    phrase {
        mf
        play arp Cmaj q
    }
}

pattern arp { up }

track melody {
    layer piano
    layer cello
    gain 0.9
    humanize 0.02
    play intro
}
```

**Text → import loader → tokenizer → parser → AST → interpreter → shaping → MIDI.**

## What is SoundScript?

SoundScript is a micro-language for writing music like code. Describe notes, chords, dynamics, patterns, and orchestration in plain text; the engine parses your script, applies musical intelligence and playback refinement, and emits a MIDI file.

## V2 Overview

V2 extends the v1.2 five-phase engine with compositional and production features:

| Feature | Syntax |
|---------|--------|
| **Imports** | `import "lib.ss"` |
| **Blocks** | `block intro { }` + `play intro` |
| **Metadata** | `gain 0.9`, `humanize 0.03` |
| **Tempo automation** | `tempo 120 → 140 over 4 bars` |
| **Layers** | `layer piano` / `layer cello` |
| **Humanization** | Deterministic timing + velocity jitter |
| **Advanced chords** | `Cmaj drop2`, `inv1`, `spread` |
| **Phrases** | `phrase { curve soft ... }` · [V3 aliases & envelopes](docs/phrases-v3.md) |
| **Patterns** | `pattern arp { up }` + `play arp Cmaj q` |
| **Orchestration** | `double octave`, `reinforce bass`, `brighten top` |

All v1.2 syntax remains valid.

## Interpreter Pipeline (V2)

```
DSL script
    ↓
ProgramLoader (imports)
    ↓
Tokenizer → Parser → AST
    ↓
Interpreter
    ├── PatternExpander (pattern play)
    ├── Chord: Voicing → AdvancedVoicing → Orchestration → Spacing
    ├── Note: Intelligence → PhraseShaper → PlaybackShaper
    ├── Layers (per-channel shaping)
    └── HumanizeApplicator (post-pass)
    ↓
MidiGenerator → output.mid
```

→ [docs/pipeline.md](docs/pipeline.md) · [docs/architecture.md](docs/architecture.md)

## V2 Examples

| Example | Demonstrates |
|---------|--------------|
| [examples/imports.ss](examples/imports.ss) | Multi-file imports |
| [examples/blocks.ss](examples/blocks.ss) | Named blocks |
| [examples/metadata.ss](examples/metadata.ss) | Gain + humanize |
| [examples/tempo-automation.ss](examples/tempo-automation.ss) | Tempo ramps |
| [examples/layers.ss](examples/layers.ss) | Instrument layers |
| [examples/humanization.ss](examples/humanization.ss) | Deterministic jitter |
| [examples/advanced-chords.ss](examples/advanced-chords.ss) | drop2, inv1, spread |
| [examples/phrases.ss](examples/phrases.ss) | Phrase engine v2 |
| [examples/phrases-v3.ss](examples/phrases-v3.ss) | Phrase engine v3 |
| [examples/patterns.ss](examples/patterns.ss) | Arp, strum, rhythm |
| [examples/orchestration.ss](examples/orchestration.ss) | Orchestration helpers |
| [examples/full-v2-showcase.ss](examples/full-v2-showcase.ss) | Combined V2 demo |

→ [docs/examples.md](docs/examples.md)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/full-v2-showcase.ss
```

## v1.2 Foundation

| Phase | Focus |
|-------|--------|
| **Phase 2** | Notation engine |
| **Phase 3** | Expressive notation |
| **Phase 1** | Stabilization |
| **Phase 4** | Musical intelligence |
| **Phase 5** | Playback quality |

→ [docs/whats-new-v1.2.md](docs/whats-new-v1.2.md)

## Architecture

```
/src
    SoundScript.Core/       # AST, TempoAutomationMap, InstrumentMap
    SoundScript.Parser/     # Tokenizer, Parser, ProgramLoader
    SoundScript.Midi/       # Interpreter, PatternExpander, PhraseShaper, ChordOrchestration
    SoundScript.Cli/        # CLI (ProgramLoader)
    SoundScript.Playground/ # Browser playground

/docs                       # V2 documentation + website
/examples                   # V2 example scripts
```

## Getting Started

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss
```

## Playground

Try SoundScript V2 in your browser:

**[soundscript.net/playground](https://soundscript.net/playground/)**

## What's New in V2

- Multi-file imports with `ProgramLoader`
- Named reusable blocks
- Track metadata: gain, humanize
- Tempo automation with linear ramps
- Instrument layers with per-channel MIDI
- Deterministic humanization
- Advanced chord voicing (drop2, inv1, spread)
- Phrase engine v2 with curves and transitions
- Pattern engine (arp, strum, rhythm)
- Orchestration helpers

→ [docs/whats-new-v2.md](docs/whats-new-v2.md)

## Documentation

| Document | Description |
|----------|-------------|
| [docs/language-reference.md](docs/language-reference.md) | Complete syntax (V2) |
| [docs/whats-new-v2.md](docs/whats-new-v2.md) | V2 changelog |
| [docs/imports.md](docs/imports.md) | Import system |
| [docs/blocks.md](docs/blocks.md) | Named blocks |
| [docs/track-metadata.md](docs/track-metadata.md) | Gain + humanize |
| [docs/tempo-automation.md](docs/tempo-automation.md) | Tempo ramps |
| [docs/layers.md](docs/layers.md) | Instrument layers |
| [docs/humanization.md](docs/humanization.md) | Deterministic jitter |
| [docs/advanced-chords.md](docs/advanced-chords.md) | Chord voicing |
| [docs/phrases.md](docs/phrases.md) | Phrase engine v2 |
| [docs/phrases-v3.md](docs/phrases-v3.md) | Phrase engine v3 |
| [docs/patterns.md](docs/patterns.md) | Pattern engine |
| [docs/orchestration.md](docs/orchestration.md) | Orchestration helpers |
| [docs/pipeline.md](docs/pipeline.md) | Interpreter pipeline |
| [docs/architecture.md](docs/architecture.md) | System architecture |
| [docs/examples.md](docs/examples.md) | Example catalog |

## License

See [LICENSE](LICENSE).
