# Advanced Chord Voicing

Post-voicing chord transforms applied per chord.

## Syntax

```ss
Cmaj drop2 q
Cmaj inv1 h
Cmaj spread q
```

Modifiers appear **after** the chord token, before duration.

## Styles

| Modifier | Effect |
|----------|--------|
| `drop2` | Lower 2nd voice from top by one octave |
| `drop3` | Lower 3rd voice from top by one octave |
| `inv1` | Move lowest voice up one octave |
| `inv2` | Move lowest two voices up one octave |
| `spread` | Raise each upper voice by one octave |

## Pipeline Position

```
ChordVoicing (Phase 1)
    ↓
AdvancedChordVoicing    ← here
    ↓
ChordOrchestration
    ↓
HarmonicSpacing (Phase 4)
```

## Example

→ [examples/advanced-chords.ss](../examples/advanced-chords.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/advanced-chords.ss
```

## Warnings

| Condition | Warning |
|-----------|---------|
| Voicing applied | `Advanced chord voicing applied` |

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [orchestration.md](orchestration.md) — track-level chord enrichment
- [stabilization.md](stabilization.md) — Phase 1 ChordVoicing
