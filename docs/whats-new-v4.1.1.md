# What's New in V4.1.1

SoundScript **V4.1.1** is a **timbre quality tuning pass** over the V4.1
cycle-accurate engine. No new modules, no architecture changes — every
improvement is additive and deterministic inside `SoundScript.Timbre`.

## The fix

V4.1 was architecturally correct but sounded flat: vowels leaned too hard on
the fundamental, formants had one fixed bandwidth for every phoneme, noise
was plain broadband hash noise, and consonant attacks used a single flat
curve. V4.1.1 tunes each stage:

```
Frame → Cycles → Harmonics (rolloff) → Formants (Q + drift) → Noise (shaped) → Transient (sharpened) → Stitch (crossfade)
```

## Tuned modules

| Module | Change |
|--------|--------|
| `CycleGenerator` | Per-phoneme harmonic rolloff curves (`exp`/`linear`/`polynomial`) |
| `FormantFilter` | `FormantQ` bandwidth control, deterministic ±3.5 Hz cycle drift, second-order nasal pole |
| `NoiseInjector` | Band-passed fricative noise, high-frequency-emphasized plosive bursts, voicing-damped broadband noise |
| `TransientModel` | Plosive-scaled attack sharpness, voiced micro-transient ripple, shaped ADSR curves |
| `CycleStitcher` | Optional equal-power crossfade across cycle (and frame) boundaries |
| `SpectralEngine` | Frame-to-frame profile smoothing via `TimbreProfile.Lerp` |
| `MidiToTimbreTimeline` | Phoneme-specific smoothing hint scaled by note position |

## Extended SoundCSS (v1.1)

| Property | Meaning |
|----------|---------|
| `harmonic-rolloff` | `exp` \| `linear` \| `polynomial` \| `default` |
| `formant-q` | Formant bandwidth divisor (higher = sharper resonance) |
| `noise-band` | Fricative noise band-pass centre (Hz) |
| `smoothing` | Frame-to-frame parameter smoothing hint (0–1) |

Existing `harmonic1`/`2`/`3`, `noise-fricative`, `noise-plosive`, and
`transient` properties are unchanged.

→ [examples/default.ssc](../examples/default.ssc) · [docs/v4.1.1-timbre-tuning.md](v4.1.1-timbre-tuning.md)

## Tests

New test classes in `TimbreTuningTests.cs`:

- `HarmonicRolloffTests`
- `FormantQTests`
- `NoiseShapingTests`
- `TransientEnvelopeTests`
- `CycleStitchSmoothingTests`
- `FrameContinuityTests`

`FullRenderTests` (SHA-256 stable WAV) continues to pass unmodified.

## Protected subsystems

Core, Parser, Interpreter, Voice, MIDI, and PhonemeComposer are **unchanged**.

## Previous releases

→ [What's new in V4.1](whats-new-v4.1.md) — cycle-accurate timbre reconstruction
→ [What's new in V4](whats-new-v4.md) — offline timbre synthesis (SoundCSS)
