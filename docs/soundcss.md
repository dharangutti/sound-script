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

## DSP mapping table

`SoundCssDspMapper.Map(pronunciation, canonicalVoice)` turns a validated
`SoundCssPronunciation` into a numeric `DspTransformPlan`:

```
DspTransformPlan {
    pitchSemitones, timeStretch, gainDb,
    eqBands[], formantShift, vibrato { rateHz, depthSemitones },
    noiseLayer, targetPitchHz
}
```

Composition is deterministic: the `persona` preset is applied first, then explicit
attributes compose on top — pitch/gain are **additive**, speed/formant are
**multiplicative**, EQ bands are **appended**, and vibrato/noise **accumulate**.
`speed` is a playback-rate factor `S`, stored as `timeStretch = 1 / S`.

| Attribute | Value | Numeric transform |
|-----------|-------|-------------------|
| `style` | sing | vibrato 5.5 Hz / 0.3, gain +1 dB, S×0.97, high-shelf +1 dB @3 kHz |
| | whisper | noise +0.5, gain −6 dB, high-shelf +3 dB @4 kHz, low-shelf −4 dB @250 Hz |
| | shout | gain +5 dB, peak +3 dB @2 kHz, high-shelf +2 dB @3.5 kHz |
| `accent` | uk | formant ×1.02, peak +1 dB @2.5 kHz |
| | india | formant ×1.04, peak +1.5 dB @3 kHz, S×1.03 |
| `speed` | fast / slow / xN | S ×1.15 / ×0.85 / ×N |
| `pitch` | +N / −N | pitch += N semitones |
| `energy` | high / medium / low | gain +4 / 0 / −4 dB |
| `timbre` | bright | high-shelf +4 dB @3.5 kHz |
| | dark | low-shelf +3 dB @250 Hz, high-shelf −3 dB @3.5 kHz |
| `gender` | male | pitch −4, formant ×0.92 |
| | female | pitch +4, formant ×1.08 |
| `age` | child | pitch +5, S×1.15, formant ×1.15 |
| | teen | pitch +2, formant ×1.05 |
| | senior | pitch −1, gain −1 dB, vibrato 4 Hz / 0.2 |
| `emotion` | happy | pitch +1, S×1.05, gain +1, vibrato 5.5 Hz / 0.15 |
| | sad | pitch −1, S×0.92, gain −2, low-shelf +2 dB @250 Hz, vibrato 4 Hz / 0.1 |
| | angry | gain +4, S×1.05, peak +3 dB @2 kHz |
| | calm | gain −1, S×0.95, high-shelf −1 dB @4 kHz |
| | excited | pitch +2, S×1.1, gain +3, vibrato 6 Hz / 0.2 |
| `breath` | low / medium / high | noise +0.1 / +0.25 / +0.45 (+high-shelf +2 dB @4 kHz) |
| `vibrato` | light / medium / strong | 5.5 Hz / 0.2, 5.5 Hz / 0.4, 6 Hz / 0.7 |

Pitch is computed **relative to the canonical `basePitchHz`** (from the Prompt 1/2
normalizer sidecar) and clamped so the target stays in the 70–500 Hz human band;
`formantShift` is likewise bounded so the first formant stays 250–1200 Hz. Final
clamps: gain ±24 dB, timeStretch 0.25–4, vibrato depth 0–2 / rate 0–12 Hz,
noise 0–1.

### Persona presets

Each persona applies a bundle of transforms as a base:

| Persona | Transforms |
|---------|-----------|
| `narrator` | gain +1 dB, S×0.95, noise +0.05, low-shelf +1 dB @300 Hz, high-shelf −1 dB @6 kHz |
| `robot` | noise +0.12, peak +3 dB @1.5 kHz, low-shelf −2 dB @400 Hz, high-shelf +1 dB @3 kHz, no vibrato |
| `soft` | gain −3 dB, noise +0.2, high-shelf −2 dB @4 kHz, vibrato 5 Hz / 0.15 |
| `bright` | gain +1 dB, high-shelf +5 dB @4 kHz, peak +2 dB @2.5 kHz |

The DSP rendering layer (`DspTransformRenderer`) applies a plan deterministically,
reusing the shared one-pole filter (EQ), resampling (pitch shift / time-stretch),
the deterministic sine LFO (vibrato), and the seeded PRNG (breath/noise) — so a
given `(input, plan, seed)` renders byte-identical audio on every platform.

### Applying word rules from the CLI

Pass the stylesheet with `--css` to a vocal/wave command. Each spoken word whose
text matches a quoted rule (case-insensitive) has its stem transformed
(map → DSP render) before mixing; words without a rule are unchanged:

```bash
# Per-word transforms on generated stems
soundscript vocal batch song.ssw --out-dir stems --engine wordbank --css style.ssc

# ...and through the wave renderer's offline TTS
soundscript wave song.ssw out.wav --offline-tts wordbank --css style.ssc

# Single phrase
soundscript vocal generate "jingle bells" --out jb.wav --engine wordbank --css style.ssc
```

Phoneme rules (`p`, `aa`, …) in the same file continue to feed the offline timbre
renderer (`render --css`); the two layers are independent and can coexist.

### Continuous rendering (`--continuous`)

By default each word is rendered and concatenated independently with a short
silence gap, which leaves audible boundaries — most noticeable in singing. Pass
`--continuous` to stitch words with cross-word DSP smoothing instead:

```bash
soundscript vocal generate "jingle bells" --out jb.wav --engine wordbank --css style.ssc --continuous
soundscript wave song.ssw out.wav --offline-tts wordbank --css style.ssc --continuous
```

The `ContinuousVocalRenderer` stage:

- **equal-power crossfade** (default 10 ms, `Math.Sqrt`-based) overlaps adjacent
  words, replacing the silence gap and softening attacks/releases;
- carries the **vibrato LFO phase** across words so vibrato doesn't reset;
- keeps a **continuous deterministic noise index** so the breath floor doesn't pop;
- applies **pitch and formant glide** toward the previous word (`PitchSmoothing`
  0.15, `FormantSmoothing` 0.2 by default) to avoid sudden jumps.

It is opt-in and deterministic: identical input renders byte-identical output on
every platform.

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
