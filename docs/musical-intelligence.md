# Musical Intelligence (Phase 4)

Phase 4 adds interpretive intelligence that refines pitch register, harmonic spacing, phrase continuity, and dynamic transitions — without changing script syntax.

## Modules

### OctaveSmoother

Reduces extreme octave jumps (>12 semitones) while preserving pitch spelling.

```
Previous: C4 (60)
Current:  C6 (84)  →  adjusted to C5 (72)
```

Warning: `Octave adjusted`

### MelodicContour

Corrects wide melodic leaps (>7 semitones) by single-octave displacement.

```
Previous: C5 (72)
Current:  A5 (81)  →  leap of 9, adjusted down one octave
```

Warning: `Melodic contour adjusted`

### HarmonicSpacing

Second-stage chord spacing after `ChordVoicing`:

- Raises all voices if root < MIDI 40
- Lowers highest voice if > MIDI 84
- Spreads chords with 4+ notes by raising the top voice

Warning: `Harmonic spacing adjusted`

### PhraseSmoother

Smooths melodic transitions at phrase and sequence boundaries using octave smoothing against the last note of the previous phrase.

Warning: `Phrase boundary smoothed`

### DynamicContext

Ramps velocity across abrupt dynamic changes (≥24 velocity points) over **3 notes**:

```
p → f  (48 → 96)
Note 1: velocity ~64
Note 2: velocity ~80
Note 3: velocity ~96
```

Warning: `Dynamic ramp applied`

## Diagram: Musical Intelligence Flow

```
EmitNote / EmitChord
    │
    ├── OctaveSmoother     (extreme jumps)
    ├── MelodicContour     (wide leaps)
    ├── HarmonicSpacing    (chords)
    ├── PhraseSmoother     (sequence boundaries)
    └── DynamicContext     (abrupt dynamic changes)
    │
    ▼
PlaybackShaper (Phase 5)
```

## NotatedNote Extensions

Phase 4 extends notes with resolved pitch data:

| Field | Description |
|-------|-------------|
| `AdjustedOctave` | Octave after intelligence adjustments |
| `AdjustedMidiNumber` | Final MIDI number after contour/spacing |
| `PhraseIndex` | Phrase counter for boundary detection |

## Examples

| File | Demonstrates |
|------|--------------|
| [melodic-contour.ss](../examples/melodic-contour.ss) | Wide melodic leaps |
| [harmonic-spacing.ss](../examples/harmonic-spacing.ss) | Wide chord voicings |
| [phrase-smoothing.ss](../examples/phrase-smoothing.ss) | Sequence phrase boundaries |
| [dynamic-ramping.ss](../examples/dynamic-ramping.ss) | Abrupt dynamic changes |

### Melodic contour

```
melody {
    C4 q
    G5 q
    C4 q
}
```

### Dynamic ramping

```
melody {
    p
    C4 q D4 q E4 q
    f
    F4 q G4 q A4 q
}
```

## Related

- [stabilization.md](stabilization.md) — Chord voicing (Phase 1)
- [playback-quality.md](playback-quality.md) — Dynamic shaping after ramping
- [pipeline.md](pipeline.md) — Full pipeline order
