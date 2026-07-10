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
    SoundScript.Prosody/    # Word-level prosody: ProsodyComposer + submodules (V5)
    SoundScript.Cli/        # Command-line runner (run + compose + prosody + render + wave)
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
| `SsPrinter` | Parser | AST → `.ss` DSL source text (V6) |
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
| `WordTokenizer` | Prosody | Text → words + syllables (V5) |
| `WordProsodyPlanner` | Prosody | Word → base pitch (category, position, stress) |
| `PhraseContourEngine` | Prosody | Sentence → phrase-level pitch ramp |
| `SyllableContourGenerator` | Prosody | Syllable → stress-driven micro-pitch |
| `ProsodyComposer` | Prosody | Facade: plain text → AST → InterpretedTrack (V5) |

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
│  V5:   ProsodyComposer   (text → AST, word/syllable pitch)│
└─────────────────────────────────────────────────────────┘
```

## Pipeline Branches (V5)

Five stages share one MIDI backbone; V4 adds a read-only audio branch and V5
adds a second text-to-AST branch alongside `PhonemeComposer`:

```
Tokenizer → Parser → AST
    ├── Interpreter        (tracks)   → InterpretedTrack[]
    ├── VocalInterpreter   (voices)   → InterpretedVocalTrack[]
    ├── PhonemeComposer    (text, V3.1) → AST → Interpreter → InterpretedTrack
    │       ├── Syllabifier      (reused from SoundScript.Voice)
    │       ├── PhonemeSplitter  (syllable → phonemes)
    │       ├── PhonemeMapper    (phoneme → gesture, pure data)
    │       ├── GestureBuilder   (gesture → NoteNode / PhraseEnvelopeNode)
    │       └── PhraseAssembler  (per-syllable PhraseNodes → ProgramNode)
    └── ProsodyComposer    (text, V5)   → AST → Interpreter → InterpretedTrack
            ├── WordTokenizer          (text → words + syllables)
            ├── WordProsodyPlanner     (word → base pitch)
            ├── PhraseContourEngine    (sentence → contour delta)
            ├── SyllableContourGenerator (syllable → micro-pitch)
            ├── ProsodyClamp           (bound the sequence)
            ├── PhonemeSplitter + PhonemeMapper.Kind/Duration (reused, timbre/rhythm only)
            └── ProsodyNoteBuilder + ProsodyPhraseAssembler (per-syllable PhraseNodes → ProgramNode)
    ↓
MidiGenerator → output.mid
    ↓
OfflineRenderer (V4, read-only) → output.wav / output.ogg
    ├── MidiToTimbreTimeline
    ├── PhonemeTimbreMapper + SoundCSSParser
    └── SpectralEngine → AudioWriter
```

The PhonemeComposer and ProsodyComposer branches both start from plain text
rather than a script: each builds a standard `ProgramNode` in code, then feeds
it through the unchanged `Interpreter` and `MidiGenerator`. The V4 timbre
branch reads that MIDI file and never modifies it, regardless of which text
branch produced it. → [text-to-melody.md](text-to-melody.md) ·
[phoneme-composer.md](phoneme-composer.md) · [word-prosody.md](word-prosody.md) ·
[v4-architecture.md](v4-architecture.md) · [v5-prosody-architecture.md](v5-prosody-architecture.md)

### V6 addendum: optional `.ss` detour

Both text branches expose their pre-interpretation `ProgramNode` via
`BuildAst` before it ever reaches `Interpreter`. V6 adds `SsPrinter`, which
taps that same `ProgramNode` and prints it back out as `.ss` source instead
of continuing straight to `Interpreter`/`MidiGenerator`:

```
PhonemeComposer.BuildAst / ProsodyComposer.BuildAst → ProgramNode
    ├── (default)      → Interpreter → MidiGenerator → output.mid
    └── (--emit-ss)    → SsPrinter → melody.ss → (Tokenizer → Parser → Interpreter → MidiGenerator) → output.mid
```

The bottom path re-enters the DSL front end (`Tokenizer`/`Parser`) exactly as
a hand-written script would — `SsPrinter` has no special-cased reader on the
other end. → [whats-new-v6.md](whats-new-v6.md)

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

- **CLI:** `dotnet run --project src/SoundScript.Cli -- run script.ss` · `... -- compose "text"` · `... -- prosody "text"` · `... -- render file.mid --css style.ssc` · `... -- wave script.ssw`
- **Website:** `docs/` → GitHub Pages → soundscript.net
- **Playground:** `dotnet publish` → `docs/playground/`

## Related

- [pipeline.md](pipeline.md)
- [text-to-melody.md](text-to-melody.md)
- [phoneme-composer.md](phoneme-composer.md)
- [word-prosody.md](word-prosody.md)
- [v5-prosody-architecture.md](v5-prosody-architecture.md)
- [whats-new-v5.md](whats-new-v5.md)
- [whats-new-v6.md](whats-new-v6.md)
- [whats-new-v7.md](whats-new-v7.md)
- [whats-new-v8.md](whats-new-v8.md)
- [whats-new-v3.1.md](whats-new-v3.1.md)
- [whats-new-v2.md](whats-new-v2.md)
