# What's New in V8

SoundScript **V8** adds **real vocal audio in exported Wave files** — your own
recordings mixed into deterministic WAV output, without relying on the browser
speech-synthesis preview.

## Vocal stems in the WAV (P3)

- **New directive:** `sample "path/to/vocal.wav" [gain=0.9] [at=<beats>]`
  — mix an external 16-bit PCM WAV (mono or stereo, any sample rate resampled
  to 44.1 kHz) at a beat position on the current track.
- **`speak` with a recording:** `speak "hello" sample="vocals/hello.wav" gain=0.95`
  — replaces synthetic phoneme tones for that phrase with your file while
  keeping beat alignment.
- **Example:** [examples/wave-vocal-stem.ssw](../examples/wave-vocal-stem.ssw) +
  [examples/vocal-stems/hello-world.wav](../examples/vocal-stems/hello-world.wav)
  (replace the WAV with your own take at 44.1 kHz).

Paths resolve relative to the script file directory (same rule as `import`).

## CLI vocal mixing (P2)

Extended `soundscript wave`:

```bash
# Full vocal stem over the mix (your recording)
dotnet run --project src/SoundScript.Cli -- wave song.ssw out.wav \
  --vocal vocal-stems/full-take.wav --vocal-at=0 --vocal-gain=0.9

# Map each speak phrase to pre-rendered files in a folder (slug filenames)
dotnet run --project src/SoundScript.Cli -- wave song.ssw out.wav \
  --tts-dir vocal-stems/
```

`--tts-dir` expects files named from speak text (`hello-world.wav` for
`speak "Hello world"`). Use this for **your own recordings** or files from an
external TTS tool — API tokens stay in your environment/tooling, not in `.ssw`
source.

## Improved synthetic prosody (fallback)

When no `sample=` is set, V8 slightly strengthens vowel formants in exported
`speak` / `voice { sing }` phoneme tones (still synthetic — not human voice).

## Playground note

The browser Playground still plays the **Web Speech API overlay** for preview
convenience. **Downloaded WAV** includes stems only when:

- you use the **CLI** with on-disk files, or
- a future Playground build bundles stems (V8 CLI is the supported path today).

Missing stem files in the Playground are skipped with `SkipMissingSamples`
instead of failing the render.

## No breaking MIDI changes

The MIDI backend still rejects `sample` / `speak sample=` with the same
wave-backend error pattern as V7. Existing `.ss` MIDI workflows are unchanged.

→ [wave-grammar.md](wave-grammar.md) · [cli.md](cli.md#wave--script-to-wav-v8) ·
[examples.md](examples.md)
