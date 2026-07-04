# What's New in V3

V3 expands phrase-level expressiveness while preserving full V2 compatibility.

## Phrase System Expansion

| Feature | Example | Doc |
|---------|---------|-----|
| **Curve aliases** | `curve gentle`, `curve aggressive` | [phrases-v3.md](phrases-v3.md) |
| **Transition aliases** | `transition sharp` | [phrases-v3.md](phrases-v3.md) |
| **New curves** | `curve swell`, `curve fade`, `curve expressive` | [phrases-v3.md](phrases-v3.md) |
| **New transitions** | `transition soft`, `transition expressive` | [phrases-v3.md](phrases-v3.md) |
| **Dynamic envelopes** | `crescendo`, `decrescendo` | [phrases-v3.md](phrases-v3.md) |
| **Phrase articulation** | `articulation legato` | [phrases-v3.md](phrases-v3.md) |
| **Timing modifiers** | `swing 0.67`, `push 0.02`, `pull 0.01` | [phrases-v3.md](phrases-v3.md) |

## Pipeline Additions

```
PhraseTimingShaper  ← V3 (before PhraseShaper)
PhraseShaper        ← extended curves + envelopes
```

## Compatibility

- V2 scripts unchanged — golden MIDI tests verify bit-identical output
- All new features are deterministic (no randomness)
- Parser aliases resolve at parse time with zero runtime overhead

## Example

→ [examples/phrases-v3.ss](../examples/phrases-v3.ss)

```ss
phrase {
    curve gentle
    transition sharp
    crescendo
    articulation legato
    swing 0.67
    mf
    C4 q E4 q G4 q
}
```

## Related

- [phrases-v3.md](phrases-v3.md) — Full V3 phrase reference
- [phrases.md](phrases.md) — V2 phrase engine
- [whats-new-v2.md](whats-new-v2.md) — V2 changelog
