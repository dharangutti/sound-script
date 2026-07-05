# SoundScript V5 Architecture — Word-Level Prosody

System overview for the `SoundScript.Prosody` subsystem, added in V5 alongside
the existing V3.1 `SoundScript.Compose` engine.

## Project Layout

```
/src
    SoundScript.Prosody/    # Word-level prosody: WordProsodyPlanner,
                            # PhraseContourEngine, SyllableContourGenerator,
                            # ProsodyComposer (V5)
```

(Every other project — `Core`, `Parser`, `Midi`, `Voice`, `Compose`,
`Timbre`, `Cli`, `Tests` — is unchanged; see [architecture.md](architecture.md)
for the full layout.)

## Component Map

| Component | Role |
|-----------|------|
| `WordUnit` / `WordTokenizer` | Text → words, each with its syllable breakdown (reuses `Syllabifier`) |
| `WordCategory` / `FunctionWords` | Content vs. function word classification |
| `StressLevel` / `StressDetector` | Per-syllable stress heuristic |
| `PhrasePosition` | Start / Middle / End classification of a word within its phrase |
| `WordPitchTable` | Declarative base-pitch band per category + position |
| `WordProsodyPlanner` | Resolves each word's base pitch, category, and stress |
| `SentenceType` / `PhraseContourEngine` | Sentence-level pitch ramp (statement/question/list) |
| `SyllableContourGenerator` | Stress-driven per-syllable micro-pitch offset |
| `ProsodyClamp` | Safety-net bound on adjacent jumps and phrase range |
| `PitchMath` | MIDI number → pitch class / accidental / octave |
| `ProsodyNoteBuilder` / `ProsodyPhraseAssembler` | Prosody pitch → AST notes/phrases |
| `ProsodyComposer` | Facade: text → AST → InterpretedTrack, mirroring `PhonemeComposer`'s shape |

## Layer Diagram

```
┌─────────────────────────────────────────────────────────┐
│                     SoundScript.Cli                      │
│                     Playground (WASM)                    │
└──────────────────────────┬──────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────┐
│  ProgramLoader → Parser → Interpreter → MidiGenerator    │
│                                                          │
│  V3.1: PhonemeComposer   (text → AST, phoneme pitch)     │
│  V4:   OfflineRenderer   (MIDI → SoundCSS → audio)       │
│  V5:   ProsodyComposer   (text → AST, word/syllable pitch)│
└─────────────────────────────────────────────────────────┘
```

## Pipeline Branch (V5)

`ProsodyComposer` is a new, independent branch alongside `PhonemeComposer` —
both start from plain text and both feed the same unmodified `Interpreter`
and `MidiGenerator`:

```
Tokenizer → Parser → AST
    ├── Interpreter        (tracks)   → InterpretedTrack[]
    ├── VocalInterpreter   (voices)   → InterpretedVocalTrack[]
    ├── PhonemeComposer    (text, V3.1) → AST → Interpreter → InterpretedTrack
    └── ProsodyComposer    (text, V5)   → AST → Interpreter → InterpretedTrack
            ├── WordTokenizer          (text → words + syllables)
            ├── WordProsodyPlanner     (word → base pitch)
            ├── PhraseContourEngine    (sentence → contour delta)
            ├── SyllableContourGenerator (syllable → micro-pitch)
            ├── ProsodyClamp           (bound the sequence)
            ├── PhonemeSplitter + PhonemeMapper.Kind  (reused, timbre/rhythm only)
            └── ProsodyNoteBuilder + ProsodyPhraseAssembler (→ ProgramNode)
    ↓
MidiGenerator → output.mid
    ↓
OfflineRenderer (V4, read-only) → output.wav / output.ogg
```

`ProsodyComposer` reuses `PhonemeSplitter` and `PhonemeMapper` (for gesture
kind/duration only) and `GestureBuilder.BuildEnvelope`, but builds its own
notes/phrases (`ProsodyNoteBuilder`/`ProsodyPhraseAssembler`) rather than
`GestureBuilder.BuildNote`/`PhraseAssembler`, since `MusicalGesture` has no
accidental field and can't carry a chromatic prosody pitch. No existing
Compose, Midi, Core, Parser, or Voice file is modified.

## Determinism

Every module is a pure function of its input — pitch tables, stress rules,
and the phrase-contour ramp are static/declarative. No seeds, no randomness,
no time- or platform-dependent state.

## Related

- [word-prosody.md](word-prosody.md) — stage-by-stage pipeline narrative
- [architecture.md](architecture.md) — full-system architecture (V4)
- [whats-new-v5.md](whats-new-v5.md) — V5 changelog
