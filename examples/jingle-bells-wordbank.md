# Jingle Bells — WordBank-only vocal demo

Rhythmic `.ssw` example that validates the WordBank vocal pipeline end-to-end:
**corpus human audio** for harvested lemmas + **G2P timbre** (`SpectralEngine`) for everything else.
No eSpeak, no composite fallback.

## Script

[jingle-bells-wordbank.ssw](jingle-bells-wordbank.ssw) — same instrumental layout as [jingle-bells-vocal.ssw](jingle-bells-vocal.ssw).

| `speak` phrase | Corpus audio | G2P timbre |
|----------------|--------------|------------|
| `Jingle bells jingle bells` | jingle ×2 | bells ×2 |
| `Jingle all the way` | jingle, way | all, the |
| `Oh what fun it is` | it | oh, what, fun, is |

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
