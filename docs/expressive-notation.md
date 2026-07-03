# Expressive Notation (Phase 3)

Phase 3 adds rests, ties, articulations, dynamics, and measure validation — all using existing keyword-free or minimal-keyword syntax.

## Rests

```
melody {
    C4 q
    rest e
    D4 q
}
```

- `rest` advances the beat clock by the given duration.
- No MIDI note is emitted.
- Duration syntax matches notes (`q`, `h`, `e`, `w`, `for N`, `:N`).

## Ties

```
melody {
    C5 q ~ C5 q
}
```

- The `~` operator ties adjacent notes of the **same pitch**.
- Durations merge into a single sustained note (2 beats in the example above).
- Mismatched pitches produce an error: `Invalid tie: pitches differ`.

## Articulations

| Articulation | Syntax | Playback effect |
|--------------|--------|-----------------|
| Staccato | `staccato C4 q` | ~47% duration, slightly softer |
| Legato | `C4 q legato` | ~97% duration |
| Accent | `accent C4 q` | ~110% velocity, ~102% duration |

Articulations may appear before or after the note token.

## Dynamics

```
melody {
    p
    C4 q
    mp
    D4 q
    mf
    E4 q
    f
    F4 q
}
```

| Marking | Name | Base velocity |
|---------|------|---------------|
| `p` | Piano | 48 |
| `mp` | Mezzo-piano | 64 |
| `mf` | Mezzo-forte | 80 |
| `f` | Forte | 96 |

Dynamics persist on the track until changed. Per-note `vN` overrides still apply before shaping.

## Measure Validation

When `time` is declared and bar lines (`|`) are used, the interpreter validates measure durations:

```
time 4/4
melody {
    C4 q E4 q G4 q |    ← complete (3 beats — warning: incomplete)
    C4 h |              ← complete (2 beats)
}
```

Warnings (non-blocking):

- `Measure incomplete: expected N beats, found M`
- `Measure excess: expected N beats, found M`

## Interpreter Flow (Expressive Notation)

```
Parse note/rest/dynamic
    ↓
Attach to NotatedNote (articulation, dynamic, tie)
    ↓
EmitRest / EmitNote / EmitChord
    ↓
PlaybackShaper (Phase 5)
    ↓
MIDI emission
```

## Examples

| File | Feature |
|------|---------|
| [rests.ss](../examples/rests.ss) | Rests |
| [ties.ss](../examples/ties.ss) | Ties |
| [articulations.ss](../examples/articulations.ss) | Articulations |
| [dynamics.ss](../examples/dynamics.ss) | Dynamics |

## Related

- [playback-quality.md](playback-quality.md) — How articulations and dynamics are shaped
- [musical-intelligence.md](musical-intelligence.md) — Dynamic ramping across abrupt changes
