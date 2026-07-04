# Timbre Engine (V4.1)

The **SoundScript.Timbre** project implements offline, deterministic audio
synthesis. MIDI remains the single source of truth for musical structure; the
timbre engine adds spectral colour on top without modifying MIDI generation.

## Modules

| Module | Responsibility |
|--------|----------------|
| `TimbreProfile` | Declarative timbre attributes per phoneme |
| `SoundCSSParser` | Parse `.ssc` files into profiles |
| `PhonemeTimbreMapper` | Built-in phoneme → profile table + CSS merge |
| `MidiToTimbreTimeline` | Read MIDI, align phonemes, build frame + cycle plans |
| `CycleGenerator` | Harmonic synthesis per pitch cycle (V4.1) |
| `FormantFilter` | Vowel resonators per cycle (V4.1) |
| `NoiseInjector` | Fricative/plosive noise per cycle (V4.1) |
| `TransientModel` | Consonant attack shaping (V4.1) |
| `CycleStitcher` | Stitch cycles into frame PCM (V4.1) |
| `SpectralEngine` | Cycle-accurate frame orchestrator |
| `OfflineRenderer` | Orchestrates MIDI → PCM → file |
| `AudioWriter` | Writes WAV and OGG Vorbis |

## Pipeline

```
MIDI file
    ↓
MidiToTimbreTimeline     extract notes, tempo, align phonemes, plan cycles
    ↓
TimbreTimeline           8 ms frames with 3–10 cycles each @ 44.1 kHz
    ↓
SpectralEngine           per-cycle harmonics → formants → noise → stitch
    ↓
AudioWriter              WAV / OGG
```

## Cycle-accurate synthesis (V4.1)

V4.0 applied one spectral snapshot per frame. V4.1 reconstructs **3–10 pitch
cycles** inside each 8 ms frame:

```
cycle_length_ms = 1000 / pitch_hz
cycle_count     = clamp(round(frame_ms / cycle_length_ms), 3, 10)
```

Per cycle:

1. **CycleGenerator** — `harmonic1`/`2`/`3` overtone series
2. **FormantFilter** — three resonators + nasal pole (stateful across cycles)
3. **NoiseInjector** — `noise-fricative` + `noise-plosive` layers
4. **TransientModel** — attack envelope from `transient`
5. **CycleStitcher** — concatenate cycles into frame PCM

→ [v4.1-cycle-synthesis.md](v4.1-cycle-synthesis.md)

## Frame timeline

`MidiToTimbreTimeline` samples the note schedule at **8 ms** frames. Each
`TimbreFramePlan` carries:

- fundamental frequency from MIDI pitch
- amplitude from MIDI velocity
- `CycleCount` and `CycleLengthMs`
- `TimbreProfile` from SoundCSS + built-in table
- phoneme label for styling

## Spectral algorithm (V4.0 baseline)

Frame-level attributes still apply across all cycles in a frame:

- **Brightness** — spectral tilt on harmonics
- **Burst** — plosive onset weighting
- **Smoothness / openness** — note envelope and vowel interpolation
- **Nasal** — nasal resonance filter

No neural nets, no random number generators, no real-time constraints.

## Offline rendering

Rendering is intentionally **slow-motion**: every frame is synthesized in full
before the next begins. This is suitable for CLI batch export and playground
preview, not low-latency performance.

```csharp
OfflineRenderer.RenderFile(
    "output.mid",
    "style.ssc",
    "speech.wav",
    new OfflineRenderer.RenderOptions { SourceText = "hello world" });
```

## Audio formats

| Extension | Encoder |
|-----------|---------|
| `.wav` | 16-bit PCM mono RIFF |
| `.ogg` | Ogg Vorbis via `OggVorbisEncoder` (fixed stream serial for determinism) |

## Protected subsystems

Timbre is a **leaf branch**. It references `SoundScript.Compose` only for
read-only phoneme alignment helpers. Core, Parser, Interpreter, Voice, MIDI,
and PhonemeComposer are unchanged.

## See also

- [SoundCSS reference](soundcss.md)
- [Cycle synthesis (V4.1)](v4.1-cycle-synthesis.md)
- [V4 architecture](v4-architecture.md)
