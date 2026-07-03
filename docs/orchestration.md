# Orchestration Helpers

Track-level chord enrichment statements.

## Syntax

```ss
track harmony {
    double octave
    reinforce bass
    brighten top
    Cmaj w
}
```

Statements are **track-scoped** and affect all subsequent chords in that track. Flags can be combined.

## Helpers

| Statement | Effect |
|-----------|--------|
| `double octave` | Add upper-octave double of each chord tone |
| `reinforce bass` | Add root one octave below |
| `brighten top` | Add top voice one octave above |

## Pipeline Position

```
ChordVoicing (Phase 1)
    ↓
AdvancedChordVoicing
    ↓
ChordOrchestration    ← here
    ↓
HarmonicSpacing (Phase 4)
```

Orchestration runs **after voicing** and **before harmonic spacing**.

## Orchestration Diagram

```
Cmaj → [60, 64, 67]
         │
         ├─ reinforce bass  → add 48
         ├─ brighten top    → add 79
         └─ double octave   → add 72, 76, 79
         │
         ▼
    HarmonicSpacing
```

## Example

→ [examples/orchestration.ss](../examples/orchestration.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/orchestration.ss
```

## Warnings

| Condition | Warning |
|-----------|---------|
| Applied | `Orchestration applied` |

## Related

- [advanced-chords.md](advanced-chords.md) — per-chord voicing
- [layers.md](layers.md) — multi-instrument tracks
