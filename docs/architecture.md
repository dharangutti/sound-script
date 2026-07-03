# SoundScript Architecture (v1.2)

System architecture for the SoundScript engine.

## Project Layout

```
sound-script/
├── src/
│   ├── SoundScript.Core/       # Shared models, AST, notation types
│   ├── SoundScript.Parser/     # Tokenizer, Parser, NotationParser
│   ├── SoundScript.Midi/       # Interpreter, shaping, MIDI export
│   ├── SoundScript.Cli/        # Command-line runner
│   ├── SoundScript.Playground/ # Blazor WASM browser playground
│   ├── SoundScript.Web/        # Local Blazor demo
│   └── SoundScript.Tests/      # Unit and integration tests
├── docs/                       # Documentation and website
├── examples/                   # Example .ss scripts
└── README.md
```

## Component Responsibilities

| Project | Role |
|---------|------|
| **SoundScript.Core** | AST nodes, `NotatedNote`, `InterpretedProgram`, instrument maps |
| **SoundScript.Parser** | Lexical analysis, parsing, notation validation |
| **SoundScript.Midi** | Interpretation, musical intelligence, playback shaping, MIDI generation |
| **SoundScript.Cli** | `soundscript run script.ss` entry point |
| **SoundScript.Playground** | Client-side WASM playground for soundscript.net |
| **SoundScript.Web** | Local development web UI |

## Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│                     SoundScript DSL                      │
└─────────────────────────┬───────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────┐
│  SoundScript.Parser                                      │
│  Tokenizer → Parser → NotationParser                     │
└─────────────────────────┬───────────────────────────────┘
                          │ ProgramNode (AST)
┌─────────────────────────▼───────────────────────────────┐
│  SoundScript.Midi — Interpreter                          │
│  ┌─────────────┐ ┌──────────────┐ ┌──────────────────┐  │
│  │ Stabilization│ │ Intelligence │ │ Playback Quality │  │
│  │ BeatMath     │ │ Contour      │ │ PlaybackShaper   │  │
│  │ ChordVoicing │ │ Spacing      │ │ DynamicShaper    │  │
│  │ GlobalClock  │ │ Phrase/Dyn   │ │ ChordBalancer    │  │
│  └─────────────┘ └──────────────┘ └──────────────────┘  │
└─────────────────────────┬───────────────────────────────┘
                          │ InterpretedProgram
┌─────────────────────────▼───────────────────────────────┐
│  MidiGenerator (DryWetMIDI) → .mid file                  │
└─────────────────────────────────────────────────────────┘
```

## Notation Model

```
NoteNode
  └── NotatedNote
        ├── PitchClass, Accidental, Octave
        ├── DurationBeats, StandardDuration
        ├── Articulation, Dynamic, IsTied     (Phase 3)
        ├── AdjustedMidiNumber, PhraseIndex   (Phase 4)
        └── ShapedVelocity, ShapedDuration    (Phase 5)
```

## Engine Phase Map

| Engine Phase | Modules | Location |
|--------------|---------|----------|
| Phase 2 — Notation | `NotatedNote`, `NotationParser` | Core, Parser |
| Phase 3 — Expressive | `RestNode`, `DynamicNode`, ties | Core, Parser, Midi |
| Phase 1 — Stabilization | `BeatMath`, `ChordVoicing`, `GlobalBeatClock` | Midi |
| Phase 4 — Intelligence | `OctaveSmoother`, `HarmonicSpacing`, `DynamicContext` | Midi |
| Phase 5 — Playback | `PlaybackShaper`, `ExpressiveCurve`, `ChordBalancer` | Midi |

## Deployment

```
GitHub Actions (push to main)
    ↓
dotnet publish SoundScript.Playground → docs/playground/
    ↓
Deploy docs/ → gh-pages → soundscript.net
```

## Related

- [pipeline.md](pipeline.md) — Interpreter and shaping pipeline details
- [language-reference.md](language-reference.md) — DSL syntax
