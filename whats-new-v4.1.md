# What's New in V4.1

SoundScript **V4.1** adds **cycle-accurate timbre reconstruction** inside the
existing `SoundScript.Timbre` subsystem. MIDI and SoundCSS remain unchanged at
the pipeline level; synthesis quality improves dramatically.

## The fix

V4.0 generated one spectral snapshot per 8 ms frame — correct envelopes, but
flat pitch perception. V4.1 reconstructs **3–10 pitch cycles per frame**:

```
Frame → Cycles → Harmonics → Formants → Noise → PCM
```

A4 at 440 Hz produces ~2.27 ms cycles; an 8 ms frame stitches ~4 complete
waveform periods with independent harmonic and formant shaping.

## New modules

| Module | Purpose |
|--------|---------|
| `CycleGenerator` | Harmonic series per pitch cycle |
| `FormantFilter` | Vowel resonators applied per cycle |
| `NoiseInjector` | Fricative/plosive noise per cycle |
| `TransientModel` | Consonant attack shaping |
| `CycleStitcher` | Cycle → frame PCM assembly |
| `CyclePlanner` | 3–10 cycle count from pitch |

## Extended SoundCSS

Cycle-level attributes:

| Property | Meaning |
|----------|---------|
| `harmonic1` | Fundamental amplitude |
| `harmonic2` | Second harmonic amplitude |
| `harmonic3` | Third harmonic amplitude |
| `noise-fricative` | Fricative noise per cycle |
| `noise-plosive` | Plosive noise per cycle |
| `transient` | Consonant attack length (ms) |

→ [examples/default.ssc](../examples/default.ssc)

## Tests

New test classes in `CycleSynthesisTests.cs`:

- `CycleGeneratorTests`
- `FormantFilterTests`
- `NoiseInjectorTests`
- `CycleStitcherTests`
- `FullRenderTests` (SHA-256 stable WAV)

## Protected subsystems

Core, Parser, Interpreter, Voice, MIDI, and PhonemeComposer are **unchanged**.

## Previous releases

→ [What's new in V4](whats-new-v4.md) — offline timbre synthesis (SoundCSS)
