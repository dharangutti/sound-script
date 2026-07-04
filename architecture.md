# SoundScript Architecture (V4)

System overview for the SoundScript engine and documentation suite.

## Project Layout

```
/src
    SoundScript.Core/       # AST, NotatedNote, TempoAutomationMap, InstrumentMap
    SoundScript.Parser/     # Tokenizer, Parser, ProgramLoader
    SoundScript.Midi/       # Interpreter, shaping, PatternExpander, ChordOrchestration
    SoundScript.Voice/      # Vocal engine: Syllabifier, LyricAligner, VocalInterpreter
    SoundScript.Compose/    # Text-to-melody: PhonemeComposer + submodules (V3.1)
    SoundScript.Timbre/     # Offline timbre: SoundCSS + SpectralEngine (V4)
    SoundScript.Cli/        # Command-line runner (run + compose + render)
    SoundScript.Playground/ # Browser playground (Blazor WASM)
    SoundScript.Web/        # Local Blazor demo
    SoundScript.Tests/      # xUnit tests

/docs                       # Documentation + website
/examples                   # Example scripts
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
| `PhonemeComposer` | Compose | Facade: plain text → AST → InterpretedTrack (V3.1) |
| `PhonemeSplitter` | Compose | Rule-based syllable → phoneme symbols |
| `PhonemeMapper` | Compose | Pure-data phoneme → musical gesture table |
| `GestureBuilder` | Compose | Gesture → existing AST nodes (articulation, envelope) |
| `PhraseAssembler` | Compose | Gestures → per-syllable PhraseNodes → program AST |
| `SoundCSSParser` | Timbre | Parse `.ssc` timbre stylesheets (V4) |
| `PhonemeTimbreMapper` | Timbre | Phoneme → TimbreProfile table + CSS merge |
| `MidiToTimbreTimeline` | Timbre | MIDI notes → frame timeline |
| `SpectralEngine` | Timbre | Deterministic formant/noise synthesis |
| `OfflineRenderer` | Timbre | MIDI + SoundCSS → WAV/OGG |

## Layer Diagram (V4)

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
│                                                          │
│  V3.1: PhonemeComposer (text → AST, reuses Interpreter)  │
│  V4:   OfflineRenderer   (MIDI → SoundCSS → audio)       │
└─────────────────────────────────────────────────────────┘
```

## Pipeline Branches (V4)

Four stages share one MIDI backbone; V4 adds a read-only audio branch:

```
Tokenizer → Parser → AST
    ├── Interpreter        (tracks)   → InterpretedTrack[]
    ├── VocalInterpreter   (voices)   → InterpretedVocalTrack[]
    └── PhonemeComposer    (text)     → AST → Interpreter → InterpretedTrack
            ├── Syllabifier      (reused from SoundScript.Voice)
            ├── PhonemeSplitter  (syllable → phonemes)
            ├── PhonemeMapper    (phoneme → gesture, pure data)
            ├── GestureBuilder   (gesture → NoteNode / PhraseEnvelopeNode)
            └── PhraseAssembler  (per-syllable PhraseNodes → ProgramNode)
    ↓
MidiGenerator → output.mid
    ↓
OfflineRenderer (V4, read-only) → output.wav / output.ogg
    ├── MidiToTimbreTimeline
    ├── PhonemeTimbreMapper + SoundCSSParser
    └── SpectralEngine → AudioWriter
```

The PhonemeComposer branch starts from plain text rather than a script: it
builds a standard `ProgramNode` in code, then feeds it through the unchanged
`Interpreter` and `MidiGenerator`. The V4 timbre branch reads that MIDI file
and never modifies it. → [text-to-melody.md](text-to-melody.md) ·
[phoneme-composer.md](phoneme-composer.md) · [v4-architecture.md](v4-architecture.md)

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

- **CLI:** `dotnet run --project src/SoundScript.Cli -- run script.ss` · `... -- compose "text"`
- **Website:** `docs/` → GitHub Pages → soundscript.net
- **Playground:** `dotnet publish` → `docs/playground/`

## Related

- [pipeline.md](pipeline.md)
- [text-to-melody.md](text-to-melody.md)
- [phoneme-composer.md](phoneme-composer.md)
- [whats-new-v3.1.md](whats-new-v3.1.md)
- [whats-new-v2.md](whats-new-v2.md)
