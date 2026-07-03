# Pattern Engine

Generate arpeggios, rhythmic figures, and strums from chord definitions.

## Syntax

**Define a pattern:**

```ss
pattern arp {
    up
}
```

**Play with a chord:**

```ss
play arp Cmaj q
```

## Pattern Types

| Body | Kind | Behavior |
|------|------|----------|
| `up` | Arpeggio | Ascending chord tones; duration split evenly |
| `down` | Arpeggio | Descending |
| `updown` | Arpeggio | Ascend then descend (palindrome) |
| `strum` | Strum | Staggered chord tones (0.05 beat offset) |
| `rhythm e e q` | Rhythm | Custom durations per voice |

## Pattern Expansion Flow

```
play arp Cmaj q
    │
    ├─► Resolve chord → voicing → advanced voicing
    ├─► PatternExpander.Expand()
    │       └─► List<NoteNode>
    └─► EmitNote (each) → full musical intelligence + shaping
```

Patterns expand into individual notes; **musical intelligence applies normally**.

## Example

→ [examples/patterns.ss](../examples/patterns.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/patterns.ss
```

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [advanced-chords.md](advanced-chords.md) — voicing on pattern chords
- [orchestration.md](orchestration.md) — track-level chord helpers
