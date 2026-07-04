# Phrase Engine V2

Phrase-level shaping with scoped dynamics, curves, and transitions.

## Syntax

```ss
phrase {
    curve soft
    transition smooth
    mf
    C4 q
    E4 q
    G4 q
}
```

## Statements Inside Phrases

| Statement | Values | Effect |
|-----------|--------|--------|
| `mf` / `f` / etc. | Dynamic markings | Scoped to phrase; restored on exit |
| `curve soft` | soft, hard, balanced (+ V3: gentle, strong, expressive, swell, fade) | Velocity curve before playback shaping |
| `transition smooth` | smooth, abrupt (+ V3: sharp, soft, expressive) | Sine envelope across phrase (smooth) or flat (abrupt) |

## Pipeline Position

```
MusicalIntelligence (contour, phrase smoothing)
    ↓
PhraseShaper          ← phrase dynamics + curve + transition
    ↓
PlaybackShaper
```

`PhraseShaper` runs **before** `PlaybackShaper`. Phrase blocks set **phrase boundaries** on exit (same as `play` blocks).

## Phrase Engine Diagram

```
┌──────────────────────────────────────┐
│ phrase {                             │
│   curve soft ──► PhraseScope.Curve   │
│   transition smooth ──► Envelope     │
│   mf ──► PhraseScope.Dynamic         │
│   C4 q ──► PhraseShaper → Playback   │
│   E4 q ──► PhraseShaper → Playback   │
│ }                                    │
│   └─► phrase boundary set            │
└──────────────────────────────────────┘
```

## Example

→ [examples/phrases.ss](../examples/phrases.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/phrases.ss
```

## Warnings

| Condition | Warning |
|-----------|---------|
| Phrase shaping | `Phrase shaping applied` |
| Phrase boundary | `Phrase smoothing applied` (next note after phrase) |

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [phrases-v3.md](phrases-v3.md) — V3 phrase expansion
- [blocks.md](blocks.md) — phrase boundaries with `play`
- [musical-intelligence.md](musical-intelligence.md) — PhraseSmoother
