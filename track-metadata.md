# Track Metadata

Per-track playback controls inside `track` bodies.

## Syntax

```ss
track piano {
    instrument piano
    gain 0.85
    humanize 0.02
    mf
    C4 q
}
```

## Statements

| Statement | Range | Effect |
|-----------|-------|--------|
| `gain N` | 0.0–1.0 | Velocity multiplier after playback shaping |
| `humanize N` | ≥ 0 | Deterministic timing + velocity jitter (see [humanization.md](humanization.md)) |
| `instrument name` | — | MIDI program (unchanged from v1.2) |

## Pipeline Position

```
PlaybackShaper → track gain → TimedNote
                              ↓
                    HumanizeApplicator (post-pass)
```

Gain applies **after** `PlaybackShaper`. Humanize applies **after** all tracks are built, before MIDI export.

## Example

→ [examples/metadata.ss](../examples/metadata.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/metadata.ss
```

## Related

- [humanization.md](humanization.md) — jitter details
- [layers.md](layers.md) — multi-instrument tracks
