# Interpreter Pipeline (V2)

End-to-end flow from SoundScript source to MIDI output.

## High-Level Pipeline

```
DSL script
    ↓
ProgramLoader (imports)     ← V2
    ↓
Tokenizer → Parser → ProgramNode (AST)
    ↓
┌────────────────────────────────────────────────────────────┐
│                      Interpreter                            │
│                                                             │
│  Register: blocks, sequences, patterns                      │
│                                                             │
│  Per statement:                                             │
│    import  → (resolved at load)                             │
│    block   → register body                                  │
│    pattern → register definition                            │
│    play    → block / sequence / pattern+chord expansion     │
│    phrase  → scoped shaping context                         │
│    note    → intelligence → phrase → playback → emit      │
│    chord   → voice → advanced → orchestration → space      │
│    layer   → add MIDI channel                               │
│                                                             │
│  Post-processing:                                           │
│    HumanizeApplicator (per track)                           │
│    measure validation                                       │
└────────────────────────────┬───────────────────────────────┘
                             ↓
                      MidiGenerator → output.mid
```

## Per-Note Pipeline (V2)

```
NoteNode
    │
    ├─► MusicalIntelligence (OctaveSmoother, MelodicContour, PhraseSmoother)
    ├─► PhraseTimingShaper              ← V3 swing / push / pull
    ├─► DynamicContext.Resolve (ramp)
    ├─► PhraseShaper                    ← V2/V3 phrase blocks
    ├─► PlaybackShaper.ShapeNote
    │       ├─ DynamicShaper
    │       ├─ ArticulationShaper
    │       ├─ InstrumentGainMap (per layer)
    │       ├─ ExpressiveCurve
    │       └─ DurationNormalizer
    ├─► track gain
    └─► TimedNote → HumanizeApplicator → MIDI
```

## Per-Chord Pipeline (V2)

```
ChordNode
    │
    ├─► ChordVoicing (Phase 1)
    ├─► AdvancedChordVoicing          ← V2 drop2, inv1, spread
    ├─► ChordOrchestration            ← V2 double octave, bass, top
    ├─► HarmonicSpacing (Phase 4)
    ├─► PlaybackShaper + ChordBalancer (per layer)
    └─► TimedNotes → HumanizeApplicator → MIDI
```

## Pattern Play Pipeline (V2)

```
play arp Cmaj q
    │
    ├─► PatternExpander.Expand(pattern, chord)
    │       ├─ ChordVoicing → AdvancedChordVoicing → HarmonicSpacing
    │       └─ NoteNode[] (arp / strum / rhythm)
    └─► EmitNote (each) → full per-note pipeline
```

## Layer Pipeline (V2)

```
track with layers [piano, cello]
    │
    EmitNote(C4)
        ├─► PlaybackShaper(piano) → channel 0
        └─► PlaybackShaper(cello) → channel 1
```

## Tempo Automation (V2)

```
tempo 120 → 140 over 4 bars
    │
    └─► TempoAutomationMap.AddRamp()
            └─► beat-accurate BeatsToMilliseconds()
```

## Warnings

| Stage | Warning |
|-------|---------|
| Import override | `Duplicate block name` |
| Block recursion | `Recursive block call detected` |
| Chord voicing | `Chord voicing adjustment applied` |
| Advanced voicing | `Advanced chord voicing applied` |
| Orchestration | `Orchestration applied` |
| Harmonic spacing | `Harmonic spacing adjustment applied` |
| Phrase shaping | `Phrase shaping applied` |
| Phrase boundary | `Phrase smoothing applied` |
| Humanization | (no warning — silent) |

## Playground Pipeline

```
Interpreter → MidiGenerator → Web Audio (local soundfont)
```

## Related

- [architecture.md](architecture.md)
- [whats-new-v2.md](whats-new-v2.md)
