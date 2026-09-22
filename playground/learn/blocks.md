# Named Blocks

Reusable musical fragments defined with `block` and invoked with `play`.

## Syntax

```ss
block intro {
    mf
    C4 q
    E4 q
    G4 q
}

track melody {
    play intro
}
```

## Behavior

- Blocks expand **inline** during interpretation
- Support notes, chords, dynamics, articulations, rests, bar lines, and nested `play`
- **Recursion is rejected** (direct or indirect)
- **Phrase boundaries** are set after each `play` (same as sequences)
- Track context (instrument, velocity, dynamics) is inherited and restored

## Block vs Sequence

| | `block` | `sequence` |
|---|---------|------------|
| Body statements | Notes, chords, dynamics, play | Full track body (instrument, tempo, etc.) |
| Recursion guard | Yes | No |
| Use case | Reusable motifs | Full arrangement sections |

## Block Expansion Flow

```
play intro
    │
    ├─► Capture track context
    ├─► Execute block body statements
    ├─► Set phrase boundary (LastPhraseMidi)
    └─► Restore track context
```

## Example

→ [examples/blocks.ss](../examples/blocks.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss
```

## Warnings

| Condition | Warning |
|-----------|---------|
| Recursive call | `Recursive block call detected: 'name'.` |

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [imports.md](imports.md) — share blocks across files
- [phrases.md](phrases.md) — phrase blocks inside tracks
