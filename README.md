# SoundScript v1.2

A tiny, deterministic music DSL that turns simple text into professional-sounding MIDI.  
Built in C# for curiosity, creativity, and play.

```
melody {
    tempo 120
    mf
    C4 q E4 q G4 q | C5 h
}
```

**Text → tokenizer → parser → AST → interpreter → playback shaping → MIDI.**

## What is SoundScript?

SoundScript is a micro-language for writing music like code. You describe notes, chords, dynamics, and articulations in plain text; the engine parses your script, applies musical intelligence and playback refinement, and emits a MIDI file. No DAW, no plugins, no audio synthesis in the engine itself.

## v1.2 Overview

Version 1.2 completes the SoundScript engine across five evolution phases:

| Phase | Focus |
|-------|--------|
| **Phase 2** | Notation engine — canonical pitch model, accidentals, duration aliases |
| **Phase 3** | Expressive notation — rests, ties, articulations, dynamics, measure validation |
| **Phase 1** | Stabilization — chord voicing, beat math, multi-track sync, instrument gain |
| **Phase 4** | Musical intelligence — contour, spacing, phrase smoothing, dynamic ramping |
| **Phase 5** | Playback quality — dynamic curves, articulation shaping, expressive velocity |

All original syntax remains valid. v1.2 adds engine depth, not breaking changes.

## Language Features

- Notes with accidentals (`C#4`, `Bb3`) and duration aliases (`q`, `h`, `e`, `w`)
- Chords (`Cmaj`, `Dm`, `G7`, `Fmaj7`)
- Rests, ties (`~`), articulations (`staccato`, `legato`, `accent`)
- Dynamics (`p`, `mp`, `mf`, `f`)
- Instruments, tempo, time signature, velocity
- Sequences, loops, multi-track blocks

See [docs/language-reference.md](docs/language-reference.md) for the full syntax reference.

## Notation Support (Phase 2)

Every note is parsed into a `NotatedNote` with pitch class, accidental, octave, and duration in beats. The notation engine validates pitch spelling and maps durations consistently.

→ [docs/notation.md](docs/notation.md)

## Expressive Notation (Phase 3)

Rests advance time without emitting notes. Ties merge durations across adjacent identical pitches. Articulations and dynamics shape playback. Bar lines trigger measure validation warnings.

→ [docs/expressive-notation.md](docs/expressive-notation.md)

## Stabilization (Phase 1)

Chord voicing raises muddy low roots and spreads wide harmonies. `BeatMath` prevents floating-point drift. `GlobalBeatClock` aligns multi-track timing. `InstrumentGainMap` balances per-instrument loudness.

→ [docs/stabilization.md](docs/stabilization.md)

## Musical Intelligence (Phase 4)

Octave smoothing and melodic contour correct extreme leaps. Harmonic spacing refines chord register. Phrase smoothing bridges sequence boundaries. Dynamic context ramps abrupt dynamic changes over three notes.

→ [docs/musical-intelligence.md](docs/musical-intelligence.md)

## Playback Quality (Phase 5)

A six-stage playback shaping pipeline refines velocity and duration before MIDI emission: dynamic curves, articulation shaping, instrument gain refinement, expressive curves, duration normalization, and chord velocity balancing.

→ [docs/playback-quality.md](docs/playback-quality.md)

## Interpreter Pipeline

```
DSL script
    ↓
Tokenizer  →  Parser  →  AST (ProgramNode)
    ↓
Interpreter
    ├── Notation resolution (NotatedNote)
    ├── Chord expansion + voicing + spacing
    ├── Musical intelligence (contour, phrase, dynamics)
    ├── Playback shaping (PlaybackShaper)
    └── Multi-track sync (GlobalBeatClock)
    ↓
MidiGenerator  →  output.mid
```

→ [docs/pipeline.md](docs/pipeline.md) · [docs/architecture.md](docs/architecture.md)

### Playback shaping pipeline

```
Base velocity (note / dynamic / track)
    ↓ DynamicShaper        (after dynamic ramp)
    ↓ ArticulationShaper   (velocity + duration)
    ↓ InstrumentGainMap    (per-instrument gain)
    ↓ InstrumentGainRefiner
    ↓ ExpressiveCurve
    ↓ DurationNormalizer
    ↓ ChordBalancer        (chords only)
    ↓ MIDI note emission
```

## Examples

| Example | Demonstrates |
|---------|--------------|
| [examples/melody.ss](examples/melody.ss) | Basic melody |
| [examples/rests.ss](examples/rests.ss) | Rests |
| [examples/ties.ss](examples/ties.ss) | Ties |
| [examples/articulations.ss](examples/articulations.ss) | Staccato, legato, accent |
| [examples/dynamics.ss](examples/dynamics.ss) | Dynamic markings |
| [examples/chord-voicing.ss](examples/chord-voicing.ss) | Chord voicing |
| [examples/harmonic-spacing.ss](examples/harmonic-spacing.ss) | Harmonic spacing |
| [examples/melodic-contour.ss](examples/melodic-contour.ss) | Melodic contour |
| [examples/phrase-smoothing.ss](examples/phrase-smoothing.ss) | Phrase boundaries |
| [examples/dynamic-ramping.ss](examples/dynamic-ramping.ss) | Dynamic ramping |
| [examples/multitrack-sync.ss](examples/multitrack-sync.ss) | Multi-track sync |
| [examples/playback-shaping.ss](examples/playback-shaping.ss) | Playback shaping |
| [examples/full.ss](examples/full.ss) | Combined showcase |

→ [docs/examples.md](docs/examples.md)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

## Architecture Overview

```
/src
    SoundScript.Core/       # AST, NotatedNote, instrument maps
    SoundScript.Parser/     # Tokenizer, Parser, NotationParser
    SoundScript.Midi/       # Interpreter, shaping modules, MIDI export
    SoundScript.Cli/        # Command-line runner
    SoundScript.Playground/ # Browser playground (Blazor WASM)
    SoundScript.Web/        # Local Blazor demo

/docs                       # Language reference, architecture, website
/examples                   # v1.2 example scripts
```

→ [docs/architecture.md](docs/architecture.md)

## Getting Started

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet build
dotnet run --project src/SoundScript.Cli -- run examples/melody.ss
```

Open `output.mid` in any MIDI player.

## Playground

Try SoundScript in your browser — no install:

**[soundscript.net/playground](https://soundscript.net/playground/)**

```bash
dotnet publish src/SoundScript.Playground/SoundScript.Playground.csproj -c Release
```

Output goes to `docs/playground/` for GitHub Pages deployment.

## What's New in v1.2

- Canonical notation model with accidentals and duration aliases
- Rests, ties, articulations, dynamics, and measure validation
- Stabilized timing, voicing, and multi-track sync
- Musical intelligence: contour, spacing, phrase smoothing, dynamic ramping
- Playback quality pipeline with expressive velocity curves
- Updated examples, diagrams, and documentation

→ [docs/whats-new-v1.2.md](docs/whats-new-v1.2.md)

## Documentation

| Document | Description |
|----------|-------------|
| [docs/language-reference.md](docs/language-reference.md) | Complete syntax reference |
| [docs/notation.md](docs/notation.md) | Notation engine (Phase 2) |
| [docs/expressive-notation.md](docs/expressive-notation.md) | Rests, ties, articulations (Phase 3) |
| [docs/stabilization.md](docs/stabilization.md) | Timing and voicing (Phase 1) |
| [docs/musical-intelligence.md](docs/musical-intelligence.md) | Contour and spacing (Phase 4) |
| [docs/playback-quality.md](docs/playback-quality.md) | Playback shaping (Phase 5) |
| [docs/pipeline.md](docs/pipeline.md) | Interpreter and shaping pipelines |
| [docs/architecture.md](docs/architecture.md) | System architecture |
| [docs/examples.md](docs/examples.md) | Example catalog |

## License

See [LICENSE](LICENSE).
