# SoundScript Architecture (V2)

System overview for the SoundScript engine and documentation suite.

## Project Layout

```
/src
    SoundScript.Core/       # AST, NotatedNote, TempoAutomationMap, InstrumentMap
    SoundScript.Parser/     # Tokenizer, Parser, ProgramLoader
    SoundScript.Midi/       # Interpreter, shaping, PatternExpander, ChordOrchestration
    SoundScript.Voice/      # Vocal engine: Syllabifier, LyricAligner, VocalInterpreter
    SoundScript.Cli/        # Command-line runner (ProgramLoader)
    SoundScript.Playground/ # Browser playground (Blazor WASM)
    SoundScript.Web/        # Local Blazor demo
    SoundScript.Tests/      # xUnit tests

/docs                       # V2 documentation + website
/examples                   # V2 example scripts
```

## Component Map

| Component | Project | Role |
|-----------|---------|------|
| `ProgramLoader` | Parser | Import resolution, AST merge |
| `Tokenizer` / `Parser` | Parser | DSL → AST |
| `Interpreter` | Midi | AST → InterpretedProgram |
| `PatternExpander` | Midi | Pattern → NoteNode[] |
| `ChordOrchestration` | Midi | Orchestration helpers |
| `PhraseShaper` | Midi | Phrase-level shaping |
| `PhraseTimingShaper` | Midi | V3 swing / push / pull timing |
| `AdvancedChordVoicing` | Midi | drop2, inv1, spread |
| `HumanizeApplicator` | Midi | Deterministic jitter |
| `TempoAutomationMap` | Core | Linear tempo ramps |
| `MidiGenerator` | Midi | InterpretedProgram → .mid |
| `Syllabifier` | Voice | Deterministic phonetic syllabification |
| `LyricAligner` | Voice | Syllable ↔ note binding (melisma, overflow) |
| `VocalInterpreter` | Voice | VoiceNode → InterpretedVocalTrack |

## Layer Diagram (V2)

```
┌─────────────────────────────────────────────────────────┐
│                     SoundScript.Cli                      │
│                     Playground (WASM)                    │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│  ProgramLoader → Parser → Interpreter → MidiGenerator    │
│                                                          │
│  V2 modules:                                             │
│    PatternExpander │ PhraseShaper │ ChordOrchestration   │
│    HumanizeApplicator │ TempoAutomationMap │ Layers      │
└─────────────────────────────────────────────────────────┘
```

## Engine Phases

| Phase | Modules | V2 additions |
|-------|---------|--------------|
| Load | ProgramLoader | Imports |
| Parse | Tokenizer, Parser | pattern, phrase, orchestration tokens |
| Voicing | ChordVoicing, AdvancedChordVoicing | drop2, inv1, spread |
| Orchestration | ChordOrchestration | double octave, bass, top |
| Intelligence | OctaveSmoother, PhraseSmoother | Phrase blocks |
| Phrase shaping | PhraseShaper | curve, transition |
| Patterns | PatternExpander | arp, strum, rhythm |
| Playback | PlaybackShaper, Layers | per-layer channels |
| Post | HumanizeApplicator | deterministic jitter |
| Export | MidiGenerator, TempoAutomationMap | tempo ramps |

## Deployment

- **CLI:** `dotnet run --project src/SoundScript.Cli -- run script.ss`
- **Website:** `docs/` → GitHub Pages → soundscript.net
- **Playground:** `dotnet publish` → `docs/playground/`

## Related

- [pipeline.md](pipeline.md)
- [whats-new-v2.md](whats-new-v2.md)
