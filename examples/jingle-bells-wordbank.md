# Jingle Bells — WordBank-only vocal demo

Rhythmic `.ssw` example that validates the WordBank vocal pipeline end-to-end:
**corpus human audio** for harvested lemmas + **G2P timbre** (`SpectralEngine`) for everything else.
No eSpeak, no composite fallback.

## Script

[jingle-bells-wordbank.ssw](jingle-bells-wordbank.ssw) — same instrumental layout as [jingle-bells-vocal.ssw](jingle-bells-vocal.ssw).

Since the wordbank corpus (v0.6.2) now covers the **full Jingle Bells word set** (66 English
pronunciations, see [wordbank CHANGELOG](https://github.com/dharangutti/soundscript-wordbank/blob/main/CHANGELOG.md)),
every word below is resolved from **corpus human audio** — G2P timbre is only a fallback for
words outside the song vocabulary.

| `speak` phrase | Corpus audio |
|----------------|--------------|
| `Jingle bells jingle bells` | jingle, bells, jingle, bells |
| `Jingle all the way` | jingle, all, the, way |
| `Oh what fun it is` | oh, what, fun, it, is |

## Render full mix (instrumental + WordBank stems)

```bash
dotnet run --project src/SoundScript.Cli -- wave examples/jingle-bells-wordbank.ssw \
  examples/jingle-bells-wordbank.wav \
  --offline-tts wordbank \
  --offline-tts-dir vocal-stems/wordbank \
  --locale en
```

## Stems only

```bash
dotnet run --project src/SoundScript.Cli -- vocal batch examples/jingle-bells-wordbank.ssw \
  --out-dir examples/vocal-stems/wordbank \
  --engine wordbank \
  --locale en
```

Paths for `--tts-dir` and `--offline-tts-dir` are relative to the script directory (`examples/` for these scripts). From the repo root, batch `--out-dir` uses a full path under `examples/`.

Pre-rendered output: [jingle-bells-wordbank.wav](jingle-bells-wordbank.wav)

See also [docs/phase8-wordbank-vocal.md](../docs/phase8-wordbank-vocal.md).
