# Humanization

Deterministic micro-timing and velocity jitter via `humanize`.

## Syntax

```ss
track piano {
    humanize 0.03
    C4 q
}
```

## Behavior

| Dimension | Formula | When applied |
|-----------|---------|--------------|
| **Timing** | ±`humanize` seconds on start beat | Before MIDI emission |
| **Velocity** | ±`humanize × 127` on shaped velocity | Before MIDI emission |

### Deterministic Seed

```csharp
seed = HashCode.Combine(seedOverride ?? 1337, noteIndex, channel)
```

Same script + same seed → identical output every run.

## Pipeline Position

```
PlaybackShaper → track gain → TimedNote
                              ↓
                    HumanizeApplicator  ← here
                              ↓
                         MidiGenerator
```

Humanization runs **after** playback shaping, not inside `PlaybackShaper`.

## Example

→ [examples/humanization.ss](../examples/humanization.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/humanization.ss
```

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [track-metadata.md](track-metadata.md) — `humanize` statement
- [pipeline.md](pipeline.md) — post-pass humanization
