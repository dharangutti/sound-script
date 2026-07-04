# SoundCSS — SoundScript Style Sheets (V4.1)

SoundCSS (`.ssc`) is a declarative timbre language for the offline
**SoundScript.Timbre** engine. It styles phonemes the way CSS styles HTML
elements: pure data, deterministic, no randomness.

## Syntax

```css
// optional phoneme sequence for MIDI alignment
@phonemes t w ai n;

p {
    burst: 12ms;
    noise-plosive: 0.4;
    harmonic1: 0.2;
    harmonic2: 0.1;
}

aa {
    formant1: 700Hz;
    formant2: 1100Hz;
    smoothness: 0.9;
    harmonic1: 0.9;
    harmonic2: 0.6;
    harmonic3: 0.3;
    noise-fricative: 0.2;
    transient: 8ms;
}
```

### Selectors

Each selector is a **phoneme symbol** from the
[PhonemeComposer mapping table](phoneme-composer.md) (`p`, `t`, `aa`, `sh`, …).

### Properties

| Property | Type | Meaning |
|----------|------|---------|
| `burst` | ms | Plosive/fricative transient length at note onset |
| `noise` | 0–1 | Noise layer mix (0 = voiced, 1 = noise) |
| `brightness` | 0–1 | High-frequency emphasis |
| `formant1` | Hz | First formant centre |
| `formant2` | Hz | Second formant centre |
| `formant3` | Hz | Third formant centre |
| `formant1bw` | Hz | First formant bandwidth |
| `formant2bw` | Hz | Second formant bandwidth |
| `formant3bw` | Hz | Third formant bandwidth |
| `smoothness` | 0–1 | Vowel transition smoothing |
| `nasal` | 0–1 | Nasal resonance amount |
| `openness` | 0–1 | Vowel openness (spectral spacing) |

### Cycle-level properties (V4.1)

Applied per pitch cycle inside each 8 ms frame:

| Property | Type | Meaning |
|----------|------|---------|
| `harmonic1` | 0–1 | Fundamental harmonic amplitude |
| `harmonic2` | 0–1 | Second harmonic amplitude |
| `harmonic3` | 0–1 | Third harmonic amplitude |
| `noise-fricative` | 0–1 | Fricative noise per cycle |
| `noise-plosive` | 0–1 | Plosive noise per cycle |
| `transient` | ms | Consonant attack transient length |

Units are optional for unitless values (`0.3`). Frequencies accept an `Hz`
suffix; `burst` and `transient` accept `ms`.

### Directives

| Directive | Purpose |
|-----------|---------|
| `@phonemes a b c` | Explicit phoneme order for MIDI note alignment |

When `@phonemes` is absent, pass `--text` to the CLI `render` verb or let the
playground supply the compose text. Without either, the engine falls back to
deterministic MIDI signature guessing.

## Built-in table

`PhonemeTimbreMapper` ships a total fallback table covering every phoneme in
`PhonemeMapper`. SoundCSS overrides merge on top of the built-in row.

## Example file

→ [examples/default.ssc](../examples/default.ssc)

## CLI usage

```bash
soundscript render output.mid --css examples/default.ssc --out speech.wav \
  --text "Twinkle twinkle little star"
```

## Determinism

Identical MIDI + SoundCSS + phoneme alignment yields identical audio bytes on
every platform. Regression tests assert SHA-256 hashes of rendered WAV output.

## See also

- [Timbre engine](timbre-engine.md)
- [Cycle synthesis (V4.1)](v4.1-cycle-synthesis.md)
- [V4 architecture](v4-architecture.md)
- [Text-to-melody pipeline](text-to-melody.md)
