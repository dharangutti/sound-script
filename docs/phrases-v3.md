# Phrase Engine V3

V3 expands the phrase system with aliases, envelopes, phrase-level articulation, and deterministic timing modifiers — without changing V2 behavior.

## What's New

| Feature | Syntax | Effect |
|---------|--------|--------|
| **Curve aliases** | `curve gentle`, `curve strong`, `curve aggressive` | Map to `soft`, `hard`, `hard` |
| **Transition aliases** | `transition sharp` | Maps to `abrupt` |
| **New curves** | `expressive`, `swell`, `fade` | Velocity shaping + position envelopes |
| **New transitions** | `soft`, `expressive` | Wider or asymmetric sine envelopes |
| **Dynamic envelopes** | `crescendo`, `decrescendo` | Ramp velocity across phrase notes |
| **Phrase articulation** | `articulation legato` | Default articulation for all phrase notes |
| **Timing** | `swing 0.67`, `push 0.02`, `pull 0.01` | Deterministic groove offsets |

## Syntax

```ss
phrase {
    curve gentle              // alias for soft
    transition sharp          // alias for abrupt
    crescendo
    articulation legato
    swing 0.67
    mf
    C4 q E4 q G4 q
}
```

## Curve Values

| Value | Alias | Effect |
|-------|-------|--------|
| `soft` | `gentle` | Rounded velocity (√ curve) |
| `hard` | `strong`, `aggressive` | Accentuated velocity (x² curve) |
| `balanced` | — | Blend of linear and soft |
| `expressive` | — | 50/50 linear + soft blend |
| `swell` | — | Linear ramp up across phrase (0.85 → 1.15) |
| `fade` | — | Linear ramp down across phrase (1.15 → 0.85) |

## Transition Values

| Value | Alias | Effect |
|-------|-------|--------|
| `smooth` | — | Sine envelope, peak at midpoint (±12%) |
| `abrupt` | `sharp` | Flat — no cross-note envelope |
| `soft` | — | Wider sine envelope (±20%) |
| `expressive` | — | Asymmetric contour, emphasis on opening |

## Envelopes

`crescendo` and `decrescendo` apply a position-indexed velocity multiplier independent of the static curve:

- **crescendo**: `0.85 + 0.30 × position`
- **decrescendo**: `1.15 − 0.30 × position`

When both an envelope keyword and a swell/fade curve are set, the envelope keyword takes precedence.

## Phrase Articulation

```ss
phrase {
    articulation staccato   // staccato | legato | accent | detached
    C4 q E4 q               // all notes get staccato shaping
    accent G4 q             // per-note override
}
```

Per-note articulations override the phrase default. `detached` maps to staccato shaping.

## Timing Modifiers

Applied deterministically before humanization:

| Keyword | Value | Effect |
|---------|-------|--------|
| `swing` | 0.0–1.0 | Delays off-beat notes (odd indices) |
| `push` | beats ≥ 0 | Starts notes earlier |
| `pull` | beats ≥ 0 | Starts notes later |

Swing formula for off-beat notes: `offset = duration × (1 − ratio) × 0.5`

## Pipeline Position

```
MusicalIntelligence
    ↓
PhraseTimingShaper    ← V3 swing / push / pull
    ↓
PhraseShaper          ← curve + envelope + transition
    ↓
PlaybackShaper        ← phrase articulation default applied here
    ↓
HumanizeApplicator
```

## V2 Compatibility

All V2 phrase scripts produce **bit-identical** MIDI output. New keywords are additive; existing `soft`/`hard`/`balanced` and `smooth`/`abrupt` semantics are unchanged.

Golden regression tests lock V2 example output in `src/SoundScript.Tests/Golden/`.

## Examples

→ [examples/phrases.ss](../examples/phrases.ss) (V2)
→ [examples/phrases-v3.ss](../examples/phrases-v3.ss) (V3 showcase)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/phrases-v3.ss
```

## Related

- [phrases.md](phrases.md) — V2 phrase engine
- [whats-new-v3.md](whats-new-v3.md) — V3 changelog
- [pipeline.md](pipeline.md) — Interpreter pipeline
- [language-reference.md](language-reference.md) — Complete syntax
