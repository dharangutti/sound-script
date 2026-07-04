# Timbre Engine (V4)

The **SoundScript.Timbre** project implements offline, deterministic audio
synthesis. MIDI remains the single source of truth for musical structure; the
timbre engine adds spectral colour on top without modifying MIDI generation.

## Modules

| Module | Responsibility |
|--------|----------------|
| `TimbreProfile` | Declarative timbre attributes per phoneme |
| `SoundCSSParser` | Parse `.ssc` files into profiles |
| `PhonemeTimbreMapper` | Built-in phoneme → profile table + CSS merge |
| `MidiToTimbreTimeline` | Read MIDI, align phonemes, build frame timeline |
| `SpectralEngine` | Formant + noise + burst synthesis per frame |
| `OfflineRenderer` | Orchestrates MIDI → PCM → file |
| `AudioWriter` | Writes WAV and OGG Vorbis |

## Pipeline

```
MIDI file
    ↓
MidiToTimbreTimeline     extract notes, tempo, align phonemes
    ↓
TimbreTimeline           8 ms frames @ 44.1 kHz
    ↓
SpectralEngine           formants, noise, bursts, nasal resonance
    ↓
AudioWriter              WAV / OGG
```

## Frame timeline

`MidiToTimbreTimeline` samples the note schedule at **8 ms** frames (within the
5–10 ms design range). Each active frame carries:

- fundamental frequency from MIDI pitch
- amplitude from MIDI velocity
- `TimbreProfile` from SoundCSS + built-in table
- phoneme label for styling

## Spectral algorithm

`SpectralEngine` is a deterministic additive/resonator synthesizer:

1. **Voiced source** — sine oscillator at MIDI pitch
2. **Formants** — three parallel second-order resonators (`formant1`–`3`)
3. **Noise layer** — deterministic hash noise mixed by `noise`
4. **Burst** — short squared envelope at onset for plosives (`burst`)
5. **Nasal pole** — complementary filter controlled by `nasal`
6. **Brightness** — high-shelf emphasis
7. **Note envelope** — attack/release shaped by `smoothness` and `openness`

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
- [V4 architecture](v4-architecture.md)
