# Notation Engine (Phase 2)

Phase 2 introduces a canonical internal notation model. Every parsed note becomes a `NotatedNote` before interpretation.

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

## Duration Aliases

| Alias | `NoteDuration` | Beats |
|-------|----------------|-------|
| `q` | Quarter | 1.0 |
| `h` | Half | 2.0 |
| `e` | Eighth | 0.5 |
| `w` | Whole | 4.0 |

Numeric forms (`for 2`, `:4`) are also supported.

## Accidentals

```
C4      →  MIDI 60
C#4     →  MIDI 61
Db4     →  MIDI 61
Bb3     →  MIDI 58
```

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
}
```

See [examples/melody.ss](../examples/melody.ss).

## Related

- [expressive-notation.md](expressive-notation.md) — Phase 3 extensions
- [language-reference.md](language-reference.md) — Syntax
