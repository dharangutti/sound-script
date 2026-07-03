# Notation Engine (Phase 2)

Phase 2 introduces a canonical internal notation model. Every parsed note becomes a `NotatedNote` before interpretation.

→ [language-reference.md](language-reference.md) — Complete syntax

## Notation Model

```
Parser input          NotationParser           NotatedNote
─────────────         ──────────────           ───────────
"C#4 q"        →      pitch + accidental  →    PitchClass: C
                       + duration alias         Accidental: Sharp
                                                Octave: 4
                                                StandardDuration: Quarter
                                                DurationBeats: 1.0
```

### NotatedNote fields

| Field | Type | Description |
|-------|------|-------------|
| `PitchClass` | `C`–`B` | Letter name |
| `Accidental` | `None`, `Sharp`, `Flat`, `Natural` | Alteration |
| `Octave` | `int` | Octave number (4 = middle C octave) |
| `StandardDuration` | `NoteDuration?` | Named duration alias |
| `DurationBeats` | `double` | Length in quarter-note beats |
| `StartTime` | `double` | Set during interpretation |
| `Articulation` | `ArticulationType?` | Phase 3 extension |
| `Dynamic` | `DynamicLevel?` | Phase 3 extension |
| `IsTied` | `bool` | Phase 3 extension |
| `ShapedVelocity` | `int?` | Phase 5 — final shaped velocity |
| `ShapedDurationBeats` | `double?` | Phase 5 — final shaped duration |

### MIDI conversion

```
midiNumber = (octave + 1) × 12 + pitchClass + accidentalOffset
```

## Accidentals

| Type | Syntax | MIDI offset |
|------|--------|-------------|
| None | `C4` | 0 |
| Sharp | `#` | +1 |
| Flat | `b`, `B`, `♭` | −1 |
| Natural | `♮` | 0 (cancels spelling) |

```
C4      →  MIDI 60
C#4     →  MIDI 61
Db4     →  MIDI 61
Bb3     →  MIDI 58
C♮4     →  MIDI 60
```

- Octave range: **0–8**.
- At most one accidental per note.
- Use `#` for sharps at lex time (`♯` is not tokenized).

## Duration Aliases

| Alias | `NoteDuration` | Beats |
|-------|----------------|-------|
| `q`, `quarter` | Quarter | 1.0 |
| `h`, `half` | Half | 2.0 |
| `e`, `eighth` | Eighth | 0.5 |
| `w`, `whole` | Whole | 4.0 |

**Default:** omitted duration = **1 beat**.

### Numeric Durations

| Syntax | Example | Beats |
|--------|---------|-------|
| `for N` | `C4 for 2` | 2.0 |
| `:N` | `G4:4` | 4.0 |
| `:N` (fractional) | `G4:0.5` | 0.5 |

### Dotted Durations

Dotted suffix notation (`q.`, `h.`) is **not supported**. Use numeric forms for dotted values:

| Intended value | Use instead |
|----------------|-------------|
| Dotted quarter (1.5 beats) | `C4 for 1.5` or `C4:1.5` |
| Dotted half (3 beats) | `C4 for 3` or `C4:3` |
| Dotted eighth (0.75 beats) | `C4 for 0.75` or `C4:0.75` |

Repeated single-letter aliases (`qq`, `ee`) are rejected.

## Ties

```
melody {
    C5 q ~ C5 q
}
```

- Operator: `~` between adjacent notes of the **same pitch**.
- Durations sum; `IsTied = true` on the merged note.
- Mismatched pitches error: `Invalid tie: pitches differ`.
- Chords cannot be tied.

→ [expressive-notation.md](expressive-notation.md) for tie behavior in the interpreter.

## Diagram: Notation Model

```
                    ┌─────────────────┐
                    │   NoteNode      │
                    │  (AST wrapper)  │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │   NotatedNote   │
                    ├─────────────────┤
                    │ PitchClass      │
                    │ Accidental      │
                    │ Octave          │
                    │ DurationBeats   │
                    │ Articulation    │── Phase 3
                    │ Dynamic         │── Phase 3
                    │ IsTied          │── Phase 3
                    │ ShapedVelocity  │── Phase 5
                    │ ShapedDuration  │── Phase 5
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │  ToMidiNumber() │
                    └─────────────────┘
```

## Example

```
melody {
    tempo 120
    C4 q
    C#4 q
    Db4 q
    F#4 h
    C4 for 1.5
    G4:0.5
}
```

See [examples/melody.ss](../examples/melody.ss) and [examples/durations.ss](../examples/durations.ss).

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [expressive-notation.md](expressive-notation.md) — Phase 3 extensions
