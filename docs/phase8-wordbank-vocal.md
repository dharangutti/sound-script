# Phase 8: Wordbank vocal stems

Deterministic, offline vocal stems from curated per-word human audio (CC0 / CC-BY) with rule-based G2P timbre fallback and optional eSpeak safety net.

## Wordbank scope (one line)

A deterministic, offline, multi-language corpus of curated per-word human audio with G2P fallback and DSP transforms, used to generate reproducible vocal stems without relying on eSpeak.

## Quick start

```bash
cd sound-script

# Default engine: composite (corpus → G2P → espeak → prosody per word)
dotnet run --project src/SoundScript.Cli -- vocal generate \
  "Hello welcome to SoundScript" \
  --out examples/vocal-stems/phase8-demo.wav \
  --locale en

# Strict wordbank-only (no espeak fallback)
dotnet run --project src/SoundScript.Cli -- vocal generate \
  "Hello xenoglottophobia" \
  --out examples/vocal-stems/phase8-mixed.wav \
  --engine wordbank --locale en

# External wordbank checkout (latest corpus harvest)
export WORDBANK_DIR=../soundscript-wordbank
dotnet run --project src/SoundScript.Cli -- vocal generate \
  "Hello test" \
  --wordbank-dir "$WORDBANK_DIR" \
  --engine composite --out /tmp/hello-test.wav
```

## Engine stack

| Engine | Behavior |
|--------|----------|
| `wordbank` | Corpus human WAV per word → G2P timbre (`SpectralEngine`) for OOV |
| `composite` | **Default.** Corpus → **eSpeak** (OOV) → G2P → prosody per word |
| `espeak` | System TTS only |
| `prosody` | Legacy synthetic phoneme blips |

Per-word resolution:

```
hello  → corpus/audio/en/hello.wav (Wikimedia Commons, CC-BY)
welcome → corpus/audio/en/welcome.wav
to, SoundScript → eSpeak when installed (intelligible speech), else G2P timbre
```

## Corpus pilot (v2026.07)

| Lemma | License | Source |
|-------|---------|--------|
| 32 pilot lemmas | CC0 / CC-BY | Wikimedia Commons (`scripts/harvest_commons_en.py`) |
| CI coverage | 26/50 en | See `fixtures/ci-50.json` overlap |

Harvest pipeline (future): Wiktionary → Wikimedia Commons / Lingua Libre → `corpus/vYYYY.MM/audio/{locale}/`.

## DSP transforms

Lemma metadata supports `trimStartMs`, `trimEndMs`, `gain`, and `pitchSemitones` applied before stem concatenation.

## Wave mix integration

```bash
dotnet run --project src/SoundScript.Cli -- wave examples/phase8-wordbank-vocal.ssw out.wav \
  --offline-tts composite --offline-tts-dir examples/vocal-stems/
```

See [examples/phase8-wordbank-vocal.ssw](../examples/phase8-wordbank-vocal.ssw).
