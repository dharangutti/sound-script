# Interpreter Pipeline (v1.2)

End-to-end flow from SoundScript source to MIDI output.

## High-Level Pipeline

```
DSL script
    ↓
┌───────────┐
│ Tokenizer │  →  Token stream
└─────┬─────┘
      ↓
┌───────────┐
│  Parser   │  →  ProgramNode (AST)
└─────┬─────┘
      ↓
┌───────────────────────────────────────────────────┐
│                  Interpreter                       │
│                                                    │
│  For each statement:                               │
│    rest  → advance beat clock                      │
│    note  → resolve notation → intelligence → shape │
│    chord → expand → voice → space → shape        │
│    track → separate MIDI track                     │
│                                                    │
│  Post-processing:                                  │
│    measure validation warnings                     │
│    multi-track sync (GlobalBeatClock)              │
└─────┬─────────────────────────────────────────────┘
      ↓
┌───────────────┐
│ MidiGenerator │  →  output.mid
└───────────────┘
```

## Per-Note Pipeline

```
NoteNode.Notation (NotatedNote)
    │
    ├─► Resolve MIDI number
    │
    ├─► Phase 4: OctaveSmoother
    ├─► Phase 4: MelodicContour
    ├─► Phase 4: PhraseSmoother (at boundaries)
    ├─► Phase 4: DynamicContext.Resolve (ramp velocity)
    │
    ├─► Phase 5: PlaybackShaper.ShapeNote
    │       ├─ DynamicShaper
    │       ├─ ArticulationShaper
    │       ├─ InstrumentGainMap
    │       ├─ InstrumentGainRefiner
    │       ├─ ExpressiveCurve
    │       └─ DurationNormalizer
    │
    └─► Emit TimedNote → track.Notes
```

## Per-Chord Pipeline

```
ChordNode
    │
    ├─► Expand intervals → MIDI numbers
    ├─► Phase 1: ChordVoicing
    ├─► Phase 4: HarmonicSpacing
    ├─► Phase 5: PlaybackShaper.ShapeChordVelocity
    ├─► Phase 5: ChordBalancer (per-voice velocities)
    │
    └─► Emit simultaneous TimedNotes
```

## Playback Shaping Pipeline (Phase 5)

```
Base velocity
    ↓
DynamicShaper          ← after DynamicContext ramp
    ↓
ArticulationShaper
    ↓
InstrumentGainMap
    ↓
InstrumentGainRefiner
    ↓
ExpressiveCurve
    ↓
DurationNormalizer
    ↓
ChordBalancer (chords)
    ↓
MIDI emission
```

## Multi-Track Sync

```
Track A: notes at beats 0, 1, 2, 3
Track B: notes at beats 0, 2
         ↓
GlobalBeatClock tracks global beat position
         ↓
Sync correction if drift > threshold
```

## Warnings (Non-Blocking)

All warnings are collected in `InterpretedProgram.Warnings` and do not stop interpretation.

| Stage | Warning |
|-------|---------|
| Measure validation | `Measure incomplete` / `Measure excess` |
| Chord voicing | `Chord voicing adjusted` |
| Harmonic spacing | `Harmonic spacing adjusted` |
| Octave / contour | `Octave adjusted` / `Melodic contour adjusted` |
| Phrase smoothing | `Phrase boundary smoothed` |
| Dynamic ramp | `Dynamic ramp applied` |
| Dynamic shaping | `Dynamic shaping applied` |
| Articulation | `Articulation shaping applied` |
| Gain refinement | `Instrument gain refinement applied` |
| Duration | `Duration normalization applied` |
| Expressive curve | `Expressive curve applied` |
| Chord balance | `Chord balance applied` |
| Multi-track sync | `Sync correction applied` |

## Timing

```
durationMs = (60_000 / tempo) × shapedDurationBeats
ticks = durationMs × 480 / (60_000 / tempo)
```

Default: **480 ticks per quarter note**.

## Playground Pipeline

In the browser playground, the pipeline extends one step further:

```
Interpreter → MidiGenerator → Web Audio (local soundfont)
```

## Related

- [architecture.md](architecture.md) — System overview
- [playback-quality.md](playback-quality.md) — Shaping module details
- [musical-intelligence.md](musical-intelligence.md) — Intelligence modules
