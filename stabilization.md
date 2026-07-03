# Stabilization (Phase 1)

Phase 1 hardens the interpreter for reliable, balanced MIDI output across chords, loops, sequences, and multi-track scripts.

## Modules

### BeatMath

Deterministic beat arithmetic prevents floating-point drift in long sequences.

```
RoundBeat(beats)  →  rounds to 9 decimal places (1e-9 grid)
AddBeats(a, b)    →  RoundBeat(a + b)
```

### ChordVoicing

Refines chord register after interval expansion:

```
Input:  [36, 40, 43]   (low root < MIDI 40)
Output: [48, 52, 55]   (raised one octave)

Input:  4+ note chord
Output: highest voice raised one octave for spread
```

Warning: `Chord voicing adjusted`

### InstrumentGainMap

Per-instrument velocity multiplier applied before playback refinement:

| Instrument | Gain |
|------------|------|
| piano | 1.0 |
| flute | 0.92 |
| bass | 1.08 |
| trumpet | 1.05 |
| ... | ... |

### GlobalBeatClock

Aligns multi-track beat positions. When tracks drift, sync correction is applied.

Warning: `Sync correction applied`

### SequenceContext

Restores interpreter state when playing sequences. Warns when sequence context is inherited unexpectedly.

Warning: `Sequence context inherited`

### Loop Alignment

Loop iterations align to the beat grid to prevent cumulative timing errors in repeated blocks.

## Diagram: Stabilization Modules

```
                    ┌──────────────────┐
                    │   Interpreter    │
                    └────────┬─────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
  ┌──────▼──────┐    ┌───────▼───────┐   ┌──────▼──────┐
  │  BeatMath   │    │ ChordVoicing  │   │ GlobalBeat  │
  │  (rounding) │    │ (register)    │   │ Clock (sync)│
  └─────────────┘    └───────────────┘   └─────────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                    ┌────────▼─────────┐
                    │ InstrumentGainMap│
                    └──────────────────┘
```

## Multi-Track Sync

```
track melody { C4 q D4 q }
track bass   { C2 h     }
         ↓
GlobalBeatClock ensures beat grid alignment
         ↓
Sync correction if drift detected
```

See [examples/multitrack-sync.ss](../examples/multitrack-sync.ss).

## Chord Voicing Example

```
tempo 120
instrument piano

melody {
    Cmaj2 q
    Fmaj2 q
}
```

Low-root chords are automatically raised for clarity.

See [examples/chord-voicing.ss](../examples/chord-voicing.ss).

## Related

- [musical-intelligence.md](musical-intelligence.md) — Harmonic spacing (Phase 4)
- [pipeline.md](pipeline.md) — Full interpreter flow
