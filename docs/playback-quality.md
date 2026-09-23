# Playback Quality (Phase 5)

Phase 5 refines MIDI velocity and duration through the pipeline below. These
legacy rules still apply to scripts without a performance declaration.

[`perform expressive`](performance-interpretation.md) adds an opt-in interpretation
after MIDI event expansion/humanization and before export. The shared planner
also serves Wave, which otherwise follows an independent AST-to-audio path.
The stages below are not a description of legacy Wave or of a synthesizer's
attack/release envelopes.

## PlaybackShaper Pipeline

```text
1. Base velocity
   note vN  OR  dynamic ramp  OR  dynamic level  OR  track velocity
        ↓
2. DynamicShaper          (dynamic-level curves)
        ↓
3. ArticulationShaper     (velocity + duration)
        ↓
4. InstrumentGainMap        (Phase 1 per-instrument gain)
        ↓
5. InstrumentGainRefiner    (soft boost, hot reduction, compression)
        ↓
6. ExpressiveCurve          (phrasing curves)
        ↓
7. DurationNormalizer       (beat-grid rounding)
        ↓
8. ChordBalancer            (chords only — per-voice velocity)
        ↓
   MIDI note emission
```

## DynamicShaper

Applied **after** `DynamicContext` ramping, **before** articulation shaping.

| Dynamic | Curve | Effect |
|---------|-------|--------|
| `p` | √ soften (pow 1.25) | Quieter, rounded |
| `mp` | mild soften (pow 1.08) | Slightly softer |
| `mf` | mild harden (pow 0.92) | Slightly louder |
| `f` | harden (pow 0.78) | Louder, more attack |

Warning: `Dynamic shaping applied`

## ArticulationShaper

| Articulation | Duration | Velocity |
|--------------|----------|----------|
| Staccato | 47% of written | ×0.92 |
| Legato | 97% of written | unchanged |
| Accent | 102% of written | ×1.10 (cap 127) |

The beat cursor still advances by the **written** duration. Thus legacy legato
has an event gap of `0.03 × written duration × 60 / BPM` seconds. A half note at
60 BPM ends at 1.94 s, with the next note at 2 s. MIDI export at 480 ticks per
quarter truncates that duration to 931 ticks, leaving 29 ticks (60.417 ms).
Whether the gap is audible also depends on the receiving synthesizer's tail.

Expressive performance can extend that half note to 2.04 s when the next pitch
is suitably connected. Its audio release then lasts at most 20 ms beyond
note-off. Repetitions, accents, and rests use different rules; see the
[performance specification and measurements](performance-interpretation.md).

Warning: `Articulation shaping applied`

## InstrumentGainRefiner

Second-stage gain normalization after `InstrumentGainMap`:

| Condition | Adjustment |
|-----------|------------|
| Velocity < 40 | ×1.08 boost |
| Velocity > 110 | ×0.95 reduction |
| Percussive instruments | Mild compression curve |

Warning: `Instrument gain refinement applied`

## ExpressiveCurve

| Curve | Formula | When used |
|-------|---------|-----------|
| SoftCurve | √(v/127) × 127 | Legato notes |
| HardCurve | (v/127)² × 127 | Accent notes |
| BalancedCurve | Cubic blend | Default (non-articulated) |

Warning: `Expressive curve applied`

## DurationNormalizer

Rounds shaped durations to the 1e-9 beat grid to prevent cumulative drift in long sequences.

Warning: `Duration normalization applied`

## ChordBalancer

Per-voice velocity adjustment for chords:

| Voice | Adjustment |
|-------|------------|
| Root | +8 velocity |
| Top note | +4 velocity |
| Inner voices | −5 velocity |

All values capped at 127.

Warning: `Chord balance applied`

## NotatedNote Output Fields

After shaping, notes carry:

- `ShapedVelocity` — final MIDI velocity
- `ShapedDurationBeats` — final duration in beats

## Diagram: Playback Shaping Pipeline

```text
                    ┌─────────────────┐
                    │  Base velocity  │
                    └────────┬────────┘
                             │
              ┌──────────────▼──────────────┐
              │       DynamicShaper         │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │    ArticulationShaper       │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │    InstrumentGainMap        │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │   InstrumentGainRefiner     │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │      ExpressiveCurve        │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │    DurationNormalizer       │
              └──────────────┬──────────────┘
                             │
              ┌──────────────▼──────────────┐
              │      ChordBalancer          │  (chords)
              └──────────────┬──────────────┘
                             │
                    ┌────────▼────────┐
                    │  TimedNote → MIDI │
                    └───────────────────┘
```

## Example

```soundscript
melody {
    f
    staccato C4 q
    legato E4 q
    accent G4 q
    Cmaj q
}
```

See [examples/playback-shaping.ss](../examples/playback-shaping.ss).

## Related

- [expressive-notation.md](expressive-notation.md) — Articulation and dynamic syntax
- [pipeline.md](pipeline.md) — Full interpreter order
