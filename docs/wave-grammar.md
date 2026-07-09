# SoundScript.Wave Grammar Reference

`.ssw` is not a new language — it is the same `.ss` grammar, tokenizer, and
parser, rendered by a different backend (`SoundScript.Wave`: AST straight to
WAV, no MIDI step). Every existing `.ss` construct is valid `.ssw` input, and
every construct on this page is valid inside an ordinary `.ss` file too — the
`.ssw` extension is a naming convention for "this script uses the wave-only
additions," not a grammar boundary the parser enforces.

This page covers only what V7 adds on top of [language-reference.md](language-reference.md):
`effect`, `speak`, and the named-parameter form of `humanize`. For notes,
durations, chords, tracks, and everything else, see the main reference.

> V7 adds these to SoundScript.Wave, a parallel rendering rail alongside the
> MIDI backend — see [whats-new-v7.md](whats-new-v7.md).

## Why a second backend instead of new MIDI grammar

Two of the three additions below (`effect`, `speak`) have no MIDI
equivalent — MIDI has no post-mix audio buffer to filter/delay, and no
concept of phoneme-level frequency. Adding them as new grammar is only
justified when a feature **cannot** be expressed with existing `.ss`
grammar **and** stays fully isolated to the wave backend. `humanize`'s
`seed=` parameter fails that first test on purpose — `humanize` already
existed, so V7 extends it instead of inventing a parallel keyword.

If a `.ss` script using any of these three is run through the MIDI backend
(`SoundScript.Cli run`, the Playground's default path), the interpreter
rejects it with a clear, named error rather than silently ignoring the
directive or crashing:

```
'effect delay' is a wave-backend directive (SoundScript.Wave, .ssw files):
the MIDI backend has no post-mix audio buffer to apply effects to.
Render this file through the wave backend instead.
```

Conversely, a `.ss` script with **none** of these three directives renders
identically through either backend — adding SoundScript.Wave changes
nothing about existing `.ss` behavior.

## `effect` — master effects chain

```ss
effect delay time=0.25 feedback=0.4 mix=0.3
effect filter type=lowpass cutoff=2000
```

- **Top-level only.** `effect` applies to the final mixed buffer, after every
  track is rendered and summed — it is not a per-track or per-note directive,
  and the parser rejects it inside a `track`/`melody`/`loop` body.
- Multiple `effect` statements chain in file order (delay, then filter, in
  the example above).

| Kind | Parameters | Notes |
|------|------------|-------|
| `delay` | `time=` (required, seconds > 0), `feedback=` (0.0–<1.0, default 0.0), `mix=` (0.0–1.0, default 0.5) | Feedback delay line; output is extended with a decaying echo tail rather than truncated at the input length |
| `filter` | `type=` (required, `lowpass` or `highpass`), `cutoff=` (required, Hz > 0) | Single-pole IIR, 6 dB/octave |

`effect reverb` and any other kind are rejected at parse time with a
message pointing at what's supported — see [Scope](#scope-shipped-vs-deferred).

## `speak` — phoneme/prosody tone mapping

```ss
speak "hello world" voice=default seed=7
```

- **Per-track.** Unlike `effect`, `speak` can appear at the top level (into
  an implicit `default` track) or inside a `track`/`sequence`/`block`/`loop`
  body, same as a note.
- `voice=` — optional, defaults to `default`. V7 supports only `default`;
  any other name is rejected with a clear error.
- `seed=` — optional non-negative integer. See [Determinism](#determinism-the-null-seed-behavior).
- The text must contain at least one letter (`speak "123"` is rejected).

`speak` expands into a sequence of `NoteEvent`s via a small, fixed
grapheme-to-phoneme table — free-form frequencies (not MIDI-quantized),
each phoneme mapped to a base frequency band with seeded tone variation
inside that band. Each phoneme's class picks its timbre rather than a single
flat sine tone: vowels stack a soft formant-ish overtone on top of the
fundamental (`aa`/`ee`/`oo`/`ai`/`au`), nasals and liquids stay a plain tone
(`m`/`n`/`ng`/`w`/`r`/`l`/`j`), and plosives/fricatives synthesize from
deterministic filtered noise instead of a tone — a short low-cutoff burst for
plosives (`p`/`t`/`k`/`b`/`d`/`g`/`ch`), a sustained higher-cutoff hiss for
fricatives (`s`/`sh`/`th`/`f`/`v`/`z`/`h`). This is a proof-of-concept
demonstrating the seed-derived-variation mechanism, not a text-to-speech
engine — see [Scope](#scope-shipped-vs-deferred) for what that deliberately
excludes.

**Preview vs. export.** The Playground also plays a live browser
speech-synthesis overlay of the `speak` text as a convenience while a script
runs (the same mechanism `voice` blocks use for lyrics) — that overlay is
browser-side only and is never captured in the exported WAV unless you use
**V8 vocal stems** (`sample` / `speak sample=` — see below). Without a stem,
the WAV contains deterministic prosody synthesis only, so the export sounds
more synthetic than the in-browser preview.

**V8 vocal stems.** When `sample=` is set on `speak`, or a `sample` directive
is used, the **exported WAV includes your recording** instead of synthetic
phoneme tones for that segment. Use the CLI with on-disk WAV files.

## `sample` — external WAV stem (V8)

```ss
sample "vocal-stems/take.wav" gain=0.9 at=0
```

- **Per-track** (same placement rules as `speak`).
- `gain=` — optional linear gain (default `1.0`).
- `at=` — optional start beat; omit to use the current track cursor.
- Path resolves relative to the script file directory.
- Supports 16-bit PCM WAV, mono or stereo, resampled to 44.1 kHz.

**`speak` with a recording:**

```ss
speak "Jingle bells" sample="vocal-stems/jingle.wav" gain=0.95 seed=7
```

When `sample=` is present, beat timing follows the usual prosody rhythm but
the audio comes from your file, not `ProsodyToneGenerator`.

## `humanize` — named-parameter form

```ss
track piano {
    humanize timing=0.02 velocity=0.1 seed=42
    C4 q D4 q E4 q
}
```

`humanize` already existed (the bare-number form, `humanize 0.02`, is
unchanged and still applies the same magnitude to both timing and velocity
jitter on both backends). V7 adds an explicit named form:

| Parameter | Range | Meaning |
|-----------|-------|---------|
| `timing=` | seconds ≥ 0 | Max timing jitter, applied symmetrically (±) |
| `velocity=` | 0.0–1.0 | Max velocity jitter as a fraction, applied symmetrically (±) |
| `seed=` | non-negative integer | Explicit jitter seed — see below |

At least one of `timing=`/`velocity=` is required (`seed=` alone has
nothing to vary). Each parameter is independent: `humanize timing=0.05`
alone jitters timing only — it does **not** fall back to also jittering
velocity by the same magnitude, unlike the bare-number form.

The MIDI backend accepts the named form too (it has its own long-standing
`HumanizeApplicator.SetSeed` process-level seed mechanism and ignores
`seed=`); only SoundScript.Wave consumes `seed=` itself.

## Determinism: the null-seed behavior

Every seed in this page (`humanize ... seed=`, `speak ... seed=`) is
**optional**, and what happens when you omit it is usually the first
question: **omitting `seed=` does not mean unseeded/random — it means the
seed is derived deterministically from content instead of being explicit.**

- `humanize` with no `seed=` derives one from the enclosing track's name
  (`DeterministicRandom.DeriveSeed(trackName)`).
- `speak` with no `seed=` derives one from the spoken text itself.

Either way, the same script renders to byte-identical output every time —
there is no code path in SoundScript.Wave that reads the system clock or
uses an unseeded `System.Random`. Two different track names (or two
different `speak` strings) will generally get two different derived seeds
and therefore two different (but each individually reproducible) takes;
an explicit `seed=` is only needed when you want to pin the take
independently of the name/text, or want two different tracks/phrases to
share the same jitter pattern on purpose.

This holds across machines, operating systems, and CPUs, not just across
runs on one machine: the shared PRNG (`DeterministicRandom`) deliberately
avoids `System.Random` (whose seeded algorithm is a runtime implementation
detail that has changed between .NET versions) in favor of integer hashing
and exact IEEE `+ - * /`, and the effects chain, mixer, and WAV writer are
all written to avoid non-deterministic float summation order. See
[whats-new-v7.md](whats-new-v7.md#determinism) for how this is verified in CI.

## Scope: shipped vs. deferred

V7 pulled three items off the wave backend's parking lot; not everything on
it shipped:

**Shipped:**
- Effects chain — `delay` and a single-pole `filter` (lowpass/highpass),
  master-only, post-mix
- Seeded jitter — the named-parameter extension of `humanize` above
- Phoneme/prosody tone mapping — `speak`, a small fixed grapheme/phoneme
  table, proof-of-concept scope
- Class-based prosody timbre — vowels get a stacked formant-ish overtone,
  plosives/fricatives synthesize from deterministic filtered noise instead
  of a flat tone (revises the earlier "formant synthesis deferred" call
  below: this is still not a multi-formant filter bank or real TTS, but the
  export no longer sounds like a single flat sine beep per phoneme)

**Explicitly deferred, not silently dropped:**
- **Reverb** — algorithmically heavier (Schroeder/Freeverb-class) than the
  V7 window allowed; `effect reverb` is rejected at parse time with a
  message pointing here rather than being accepted and ignored
- **Per-track effect routing** — V7 ships master-only effects; the grammar
  and DSP would carry over directly to a per-track variant if a concrete use
  case shows up
- **Multi-language or production-quality phoneme/prosody modeling, a real
  multi-formant filter bank** — the current `speak` table is deliberately
  small and English-ish, built to prove the tone-variation mechanism (plus a
  crude per-class timbre), not to compete with a real TTS engine
- **Steeper filter slopes** (biquad/multi-pole) — V7 ships single-pole,
  6 dB/octave only
- **Additional `speak` voices** — only `voice=default` is accepted; other
  names fail with a clear error rather than silently falling back to it

Nothing above moves off this list without a concrete use case — see
[whats-new-v7.md](whats-new-v7.md) for the reasoning this scope followed.

## Where you can use this today

The [Playground](https://soundscript.net/playground/) runs `effect`/`speak`/
named-`humanize` scripts directly — `Run` detects the wave-only grammar and
renders through SoundScript.Wave instead of MIDI automatically (see
[PLAYGROUND.md](PLAYGROUND.md)). From the CLI, use the `wave` verb:

```bash
dotnet run --project src/SoundScript.Cli -- wave examples/wave-effects.ssw output.wav
```

You can also drive `SoundScript.Wave.WaveRenderer` directly as a library.

## Examples

→ [examples/wave-effects.ssw](../examples/wave-effects.ssw) — combined
`humanize` + `speak` + `effect` demo. Paste into the
[Playground](https://soundscript.net/playground/) editor and hit **Run**
(or load it via the "Wave (.ssw)" example presets, including **Combined
(wave-effects.ssw)**). From the CLI:

```bash
dotnet run --project src/SoundScript.Cli -- wave examples/wave-effects.ssw output.wav
```

See also [examples/wave-speak.ssw](../examples/wave-speak.ssw),
[examples/wave-humanize.ssw](../examples/wave-humanize.ssw), and
[examples/full-song-wave.ss](../examples/full-song-wave.ss) (standard `.ss`
rendered through the wave backend).

## Related

- [language-reference.md](language-reference.md) — complete `.ss` syntax
- [humanization.md](humanization.md) — bare-number `humanize` (unchanged)
- [whats-new-v7.md](whats-new-v7.md) — V7 changelog
- [PLAYGROUND.md](PLAYGROUND.md) — in-browser verification checklist
