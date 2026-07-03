# SoundScript Language Reference (v1.2)

Complete syntax reference for the SoundScript DSL. Whitespace separates tokens. Statements appear at the top level or inside blocks.

## Notes & Melody Blocks

```
melody {
    tempo 120
    C4 q E4 q G4 q | C5 h
}
```

**Note format:** `[A–G][#|b]?[octave]`

| Example | Meaning |
|---------|---------|
| `C4` | Middle C (MIDI 60) |
| `F#4` | F sharp, octave 4 |
| `Bb3` | B flat, octave 3 |

Pitch letters are case-insensitive.

## Bar Separator

```
C4 E4 | G4 C5
```

The `|` token marks measure boundaries. In v1.2, bar lines trigger **measure validation warnings** when note durations do not match the declared time signature.

## Durations

Durations are measured in **beats** (quarter-note beats at the current tempo).

| Syntax | Beats |
|--------|-------|
| `C5` | 1 (default) |
| `C4 for 2` | 2 |
| `G4:4` | 4 |
| `C4 q` | 1 (quarter) |
| `D4 h` | 2 (half) |
| `E4 e` | 0.5 (eighth) |
| `F4 w` | 4 (whole) |

`tempo` and legacy `bpm` both set beats per minute (default **120**).

## Instruments

```
instrument piano
instrument flute
```

| Instrument | GM Program |
|------------|------------|
| `piano` | 0 |
| `bass` | 32 |
| `violin` | 40 |
| `flute` | 73 |
| `guitar` | 24 |
| `trumpet` | 56 |
| `cello` | 42 |
| `organ` | 19 |
| `synth` | 80 |

Default: acoustic grand piano (program 0).

## Tempo & Time Signature

```
tempo 120
time 4/4
time 3/4
```

`time` sets MIDI time-signature metadata and enables measure validation warnings.

## Chords

```
Cmaj q
Dm h
G7 q
Fmaj7 w
```

| Syntax | Quality | Intervals (semitones) |
|--------|---------|----------------------|
| `Cmaj`, `C` | Major | 0, 4, 7 |
| `Dm` | Minor | 0, 3, 7 |
| `Cdim` | Diminished | 0, 3, 6 |
| `Caug` | Augmented | 0, 4, 8 |
| `G7` | Dominant 7th | 0, 4, 7, 10 |
| `Fmaj7` | Major 7th | 0, 4, 7, 11 |

Optional octave suffix: `Cmaj4`, `Dm5` (default octave: 4).

**Backward compatibility:** `G7` alone inside a `melody` block is parsed as **G at octave 7**. Write `G7 q` to play a G dominant 7th chord.

## Sequences & Blocks

```
sequence intro {
    C4 q
    D4 q
}

play intro
```

## Loops

```
loop 4 {
    C4 q
    D4 q
}
```

Repeats the block **N** times.

## Velocity

```
velocity 80
C4 q v100
```

- `velocity N` sets the track default (1–127, default **64**).
- `vN` on a note overrides for that note.

## Multi-Track

```
track melody {
    instrument flute
    C5 q
}

track bass {
    instrument bass
    C2 h
}
```

Each `track` block becomes a separate MIDI track.

## Rests (v1.2)

```
rest q
rest e
```

Advances the beat clock without emitting a note.

## Ties (v1.2)

```
C5 q ~ C5 q
```

Merges durations for the same pitch into a single sustained note. Tied pitches must match.

## Articulations (v1.2)

| Keyword | Position |
|---------|----------|
| `staccato C4 q` | Before note |
| `C4 q legato` | After note |
| `accent C4 q` | Before note |

## Dynamics (v1.2)

| Marking | Level | Base velocity |
|---------|-------|---------------|
| `p` | Piano | 48 |
| `mp` | Mezzo-piano | 64 |
| `mf` | Mezzo-forte | 80 |
| `f` | Forte | 96 |

Dynamics apply to subsequent notes until changed.

## AST Node Types

| Node | Purpose |
|------|---------|
| `ProgramNode` | Root container |
| `MelodyNode` | Legacy melody block |
| `TrackNode` | Named multi-track block |
| `SequenceNode` | Reusable sequence |
| `PlayNode` | Inline sequence expansion |
| `LoopNode` | Repeat block |
| `InstrumentNode` | MIDI program change |
| `TempoNode` / `BpmNode` | Tempo |
| `TimeSignatureNode` | Time signature |
| `VelocityNode` | Track velocity |
| `DynamicNode` | Dynamic marking |
| `NoteNode` | Single note (`NotatedNote`) |
| `ChordNode` | Chord |
| `RestNode` | Rest |
| `BarNode` | Measure boundary |

## Timing Formula

```
durationMs = (60_000 / tempo) × durationBeats
```

MIDI generator uses **480 ticks per quarter note**.

## Related Documentation

- [notation.md](notation.md) — Notation engine internals
- [expressive-notation.md](expressive-notation.md) — Rests, ties, articulations
- [pipeline.md](pipeline.md) — Interpreter and shaping flow
- [examples.md](examples.md) — Runnable examples
