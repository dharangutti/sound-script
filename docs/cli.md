# CLI Reference

The SoundScript CLI (`SoundScript.Cli`, assembly name `soundscript`) compiles
scripts and text to standard MIDI files. It requires the .NET 8 SDK and runs on
Windows, macOS, and Linux.

```bash
dotnet run --project src/SoundScript.Cli -- <verb> <arguments>
```

## Verbs

| Verb | Purpose |
|------|---------|
| `run` | Compile a `.ss` script to MIDI |
| `compose` | Compose plain text to MIDI via the [PhonemeComposer](phoneme-composer.md) (V3.1) |

## `run` — compile a script

```
soundscript run <script.ss> [output.mid]
```

```bash
dotnet run --project src/SoundScript.Cli -- run examples/full-v2-showcase.ss
dotnet run --project src/SoundScript.Cli -- run examples/vocal-song.ss vocal-song.mid
```

- Resolves imports via `ProgramLoader`, interprets tracks and voices, writes MIDI.
- `output.mid` defaults to `output.mid` in the current directory.
- Compiler warnings print to stderr; they are informational and non-blocking.

Output:

```
Wrote 24 notes across 1 track(s) and 14 sung syllable(s) across 1 voice(s) to vocal-song.mid at 100 BPM.
```

## `compose` — text to melody (V3.1)

```
soundscript compose "<text>" [output.mid] [--append <script.ss>]
```

### Standalone

Composes the text into its own MIDI file at 96 BPM:

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

### `--append` — add the composed track to a script

Compiles the script first (imports, tracks, voices — exactly like `run`),
then appends the composed `phonemes` track to the same MIDI file, using the
script's tempo:

```bash
dotnet run --project src/SoundScript.Cli -- compose "How I wonder what you are" out.mid --append examples/vocal-song.ss
```

```
Composed 7 syllable(s) and appended the phoneme track to examples/vocal-song.ss: 41 note(s) across 2 track(s) to out.mid at 100 BPM.
```

The script itself is not modified — the composed track exists only in the
generated MIDI.

### Determinism

Identical text produces identical MIDI bytes on every platform:

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" a.mid
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" b.mid
sha256sum a.mid b.mid   # identical hashes
```

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | MIDI written successfully |
| `1` | Usage error, missing file, empty compose text, or compile error (message on stderr) |

## Related

- [text-to-melody.md](text-to-melody.md) — how `compose` works
- [phoneme-composer.md](phoneme-composer.md) — module reference
- [language-reference.md](language-reference.md) — script syntax for `run`
- [examples.md](examples.md) — example catalog
