# SoundCSS — SoundScript Style Sheets (V4.1.1)

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
    noise-fricative: 0.1;
    noise-band: 6000Hz;
    transient: 4ms;
    harmonic-rolloff: exp;
    formant-q: 1.4;
    smoothing: 0.3;
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

### Timbre tuning properties (V4.1.1)

Additive quality-tuning controls layered on top of the cycle-level
properties above:

| Property | Type | Meaning |
|----------|------|---------|
| `harmonic-rolloff` | `exp` \| `linear` \| `polynomial` \| `default` | Overtone amplitude curve above the fundamental |
| `formant-q` | number | Formant bandwidth divisor — higher narrows resonance (sharper vowels), lower widens it (looser consonants) |
| `noise-band` | Hz | Fricative noise band-pass centre frequency |
| `smoothing` | 0–1 | Frame-to-frame parameter smoothing hint (0 = snap, 1 = max smoothing) |

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

## Word-level pronunciation

Beyond phoneme selectors, SoundCSS supports **word rules** that style a whole
spoken word. A word rule uses a **quoted selector**; phoneme rules (bare symbols
like `p`, `aa`) are untouched, so existing stylesheets keep working.

```css
"twinkle" {
    style: sing;
    pitch: +2;
    accent: uk;
    vibrato: light;
}

"star" {
    style: whisper;
    energy: low;
    breath: high;
}
```

### Word attributes

| Attribute | Values | Meaning |
|-----------|--------|---------|
| `style` | `normal` \| `sing` \| `whisper` \| `shout` | Delivery style |
| `accent` | `usa` \| `uk` \| `india` | Regional accent target |
| `speed` | `fast` \| `slow` \| `x1.2` \| `x0.8` | Rate preset or explicit multiplier (`0.1 < N ≤ 10`) |
| `pitch` | `+N` \| `-N` | Pitch offset in semitones (`−24..24`) |
| `energy` | `high` \| `medium` \| `low` | Loudness/effort |
| `timbre` | `bright` \| `dark` \| `flat` | Spectral colour |
| `gender` | `male` \| `female` \| `neutral` | Voice gender target |
| `age` | `child` \| `teen` \| `adult` \| `senior` | Voice age target |
| `persona` | `narrator` \| `robot` \| `soft` \| `bright` | Named persona preset |
| `emotion` | `happy` \| `sad` \| `angry` \| `calm` \| `excited` | Emotional colouring |
| `breath` | `none` \| `low` \| `medium` \| `high` | Breathiness amount |
| `vibrato` | `none` \| `light` \| `medium` \| `strong` | Vibrato depth |

Invalid values are rejected at parse time with a clear error listing the allowed
tokens. All attributes are optional; unset ones are simply absent from the plan.

### Persona presets

`persona` selects a bundled voice character. It composes with the other
attributes — set a persona for the overall character, then fine-tune:

```css
// A calm storyteller
"once" {
    persona: narrator;
    speed: slow;
    energy: medium;
}

// A robotic refrain
"initialize" {
    persona: robot;
    pitch: -2;
    vibrato: none;
    timbre: flat;
}
```

### Transform plans

Each word rule is validated into a `SoundCssPronunciation`, which the parser
projects into a deterministic **`TransformPlan`** — an ordered, DSP-agnostic list
of directives consumed by the rendering pipeline:

```csharp
var plans = SoundCSSParser.ParseTransformPlans(source);
TransformPlan twinkle = plans["twinkle"];
// twinkle.Directives → [Style=sing, Accent=uk, Pitch=+2 (Numeric 2), Vibrato=light]
```

Directives are always emitted in a fixed order (`style, accent, speed, pitch,
energy, timbre, gender, age, persona, emotion, breath, vibrato`), so identical
input yields byte-identical plans. Mapping these directives to concrete DSP
parameters happens in the DSP mapping layer.

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
- [Timbre tuning (V4.1.1)](v4.1.1-timbre-tuning.md)
- [V4 architecture](v4-architecture.md)
- [Text-to-melody pipeline](text-to-melody.md)
