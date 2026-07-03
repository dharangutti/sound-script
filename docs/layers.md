# Instrument Layers

Duplicate notes across multiple instruments within a single track.

## Syntax

```ss
track piano {
    layer piano
    layer cello
    mf
    Cmaj h
}
```

## Behavior

- Each `layer` adds an instrument with its own MIDI channel (0, 1, 2…)
- **Identical notes** are emitted per layer
- `PlaybackShaper` runs **independently** per layer instrument
- Program changes are assigned per channel

## Layer Pipeline

```
EmitNote / EmitChord
    │
    ├─► Layer 0 (piano)  → PlaybackShaper(piano)  → channel 0
    ├─► Layer 1 (cello)  → PlaybackShaper(cello)  → channel 1
    └─► ...
```

## Example

→ [examples/layers.ss](../examples/layers.ss)

```bash
dotnet run --project src/SoundScript.Cli -- run examples/layers.ss
```

## Supported Instruments

`piano`, `violin`, `flute`, `bass`, `guitar`, `trumpet`, `cello`, `organ`, `synth`

## Related

- [language-reference.md](language-reference.md) — Complete syntax
- [track-metadata.md](track-metadata.md) — gain and humanize per track
- [playback-quality.md](playback-quality.md) — per-instrument shaping
