# What's New in V9

SoundScript **V9** brings the **WordBank vocal engine** — offline, deterministic
human-audio vocalization built from a curated CC0/CC-BY corpus plus rule-based
G2P timbre — into the **Playground**, and completes the corpus with a full
song's worth of words.

## WordBank vocal engine, front and center

V9 promotes the `wordbank` / `composite` vocal engines (CLI since Phase 8) from
a docs-only feature to a first-class Playground preset and a fully corpus-backed
example:

- **New Playground preset:** *Jingle Bells + WordBank vocal (V9)* under both the
  main **Example** dropdown and the **Wave (.ssw)** pane — see
  [examples/jingle-bells-wordbank.ssw](../examples/jingle-bells-wordbank.ssw).
- **Full Jingle Bells word set in the corpus:** the embedded WordBank corpus
  grows from 32 to **66** English pronunciations
  ([soundscript-wordbank CHANGELOG](https://github.com/dharangutti/soundscript-wordbank/blob/main/CHANGELOG.md)),
  covering every word in "Jingle Bells" (chorus + verse 1). Every word in the
  bundled example — `jingle`, `bells`, `all`, `the`, `way`, `oh`, `what`, `fun`,
  `it`, `is` — now resolves from **corpus human audio**, not G2P fallback.
- **Pre-rendered demo:** [examples/jingle-bells-wordbank.wav](../examples/jingle-bells-wordbank.wav)
  + stems in [examples/vocal-stems/wordbank/](../examples/vocal-stems/wordbank/).

## Engine stack recap

| Engine | Per-word resolution order |
|--------|---------------------------|
| `wordbank` | corpus human audio → G2P timbre (`SpectralEngine`) |
| `composite` (default) | corpus → eSpeak (if installed) → G2P → prosody |
| `espeak` | system TTS only |
| `prosody` | legacy synthetic phoneme blips |

```bash
# WordBank-only stems, then full mix
dotnet run --project src/SoundScript.Cli -- vocal batch examples/jingle-bells-wordbank.ssw \
  --out-dir examples/vocal-stems/wordbank --engine wordbank --locale en
dotnet run --project src/SoundScript.Cli -- wave examples/jingle-bells-wordbank.ssw \
  examples/jingle-bells-wordbank.wav --tts-dir vocal-stems/wordbank --locale en
```

See [phase8-wordbank-vocal.md](phase8-wordbank-vocal.md) for the full engine
architecture (unchanged from Phase 8 — V9 is the Playground/corpus milestone
on top of it).

## CLI path-resolution fix

`wave --tts-dir` and `wave --offline-tts-dir` now resolve relative paths
against the **script directory** (matching the documented default), fixing a
doubled-path bug when combining `wave <script-in-subdir>` with a relative stem
folder.

## Playground note

The Playground still renders `speak` phrases through the synthetic prosody
tone (or the Web Speech API preview) — it does not link the WordBank corpus
or eSpeak into the browser build. The new preset's CLI panel gives the exact
commands to hear the real corpus-backed vocal locally.

## No breaking changes

Existing `.ss`/`.ssw` scripts, the MIDI pipeline, and the `composite`/`espeak`/
`prosody` vocal engines are unchanged. `wordbank` was already available since
Phase 8; V9 just gives it corpus coverage for a real song and Playground
visibility.

→ [phase8-wordbank-vocal.md](phase8-wordbank-vocal.md) ·
[examples.md](examples.md) · [cli.md](cli.md#vocal--offline-stem-generation-v8)
