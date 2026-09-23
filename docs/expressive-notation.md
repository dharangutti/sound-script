# Expressive Notation (Phase 3)

Phase 3 adds rests, ties, articulations, dynamics, and measure validation — all using existing keyword-free or minimal-keyword syntax.

→ [language-reference.md](language-reference.md) — Complete syntax

## Rests

```soundscript
melody {
    C4 q
    rest e
    D4 q
}
```

- `rest` advances the beat clock by the given duration.
- No MIDI note is emitted.
- Duration syntax matches notes (`q`, `h`, `e`, `w`, `quarter`, `half`, `eighth`, `whole`, `for N`, `:N`).

## Ties

```soundscript
melody {
    C5 q ~ C5 q
}
```

- The `~` operator ties adjacent notes of the **same pitch**.
- Durations merge into a single sustained note (2 beats in the example above).
- Multiple ties chain: `C5 q ~ C5 q ~ G5 q` (last segment must match pitch or error).
- Mismatched pitches produce an error: `Invalid tie: pitches differ`.
- Chords cannot be tied.

## Articulations

| Articulation | Syntax | Legacy MIDI playback effect |
|--------------|--------|-----------------|
| Staccato | `staccato C4 q` | ~47% duration, slightly softer |
| Legato | `C4 q legato` | ~97% duration |
| Accent | `accent C4 q` | ~110% velocity, ~102% duration |

Articulations may appear before or after the note token. One articulation per note.

## Slurs

SoundScript does **not** have a separate slur token or AST node. Expressive continuity is expressed through:

| Mechanism | Syntax | Effect |
|-----------|--------|--------|
| **Tie** | `C5 q ~ C5 q` | Merge durations into one sustained note |
| **Legato** | `C4 q legato` | ~97% duration per note |
| **Phrase block** | `phrase { ... }` | Scoped dynamics, curve, transition |

`curve soft` shapes velocity; it does not join distinct pitches. Legacy `legato`
shortens each note to 97% of its written duration. At 60 BPM that leaves 30 ms
after a quarter note or 60 ms after a half note, before MIDI tick quantization.
Use the top-level [`perform expressive`](performance-interpretation.md) opt-in
for controlled connections across suitable pitches. It does not connect across
rests, repeated pitches, explicit phrase boundaries, or staccato/accent attacks.

## Dynamics

```soundscript
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

## Phrase Boundaries

There is no `phrase-boundary` keyword. Boundaries are **implicit**, set when:

1. A `phrase { }` block exits
2. A `play <block>` finishes
3. A `play <sequence>` finishes

The next emitted note may trigger `PhraseSmoother` → warning `Phrase smoothing applied`.

That legacy smoother adjusts pitch/octave; it is not a release envelope or
crossfade. Expressive performance preserves written melodic pitch and applies
its own phrase velocity and release rules. Legacy Wave does not use this MIDI
smoother.

**Not** set after: plain notes, `play <pattern>`, loop end, or track/melody end.

→ [phrases.md](phrases.md) · [blocks.md](blocks.md) · [musical-intelligence.md](musical-intelligence.md)

## Measure Validation

When `time` is declared and bar lines (`|`) are used, the interpreter validates measure durations:

```text
time 4/4
melody {
    C4 q E4 q G4 q |    ← incomplete (3 beats — warning)
    C4 h |              ← complete (2 beats)
}
```

Warnings (non-blocking):

- `Measure N incomplete: expected X beats, got Y`
- `Measure N exceeds expected duration`

## Interpreter Flow (Expressive Notation)

```text
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

- [language-reference.md](language-reference.md) — Complete syntax
- [notation.md](notation.md) — Notation model (Phase 2)
- [playback-quality.md](playback-quality.md) — How articulations and dynamics are shaped
- [musical-intelligence.md](musical-intelligence.md) — Dynamic ramping across abrupt changes
