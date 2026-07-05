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
| `prosody` | Compose plain text to MIDI via the word-level [ProsodyComposer](word-prosody.md) (V5) |
| `render` | Offline timbre synthesis: MIDI + SoundCSS → WAV/OGG ([V4](whats-new-v4.md)) |

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
soundscript compose "<text>" [output.mid] [--append <script.ss>] [--emit-ss <path.ss>]
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

### `--emit-ss` — export the composed AST as `.ss` source (V6)

Writes the pre-interpretation AST that `compose` would otherwise feed
straight into the MIDI generator out as human-formatted `.ss` DSL source, in
addition to the `.mid` file (default behavior without the flag is
unchanged):

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" --emit-ss twinkle.ss
dotnet run --project src/SoundScript.Cli -- run twinkle.ss twinkle-viass.mid
```

`twinkle-viass.mid` is byte-identical to what `compose "Twinkle twinkle
little star" twinkle.mid` (no flag) produces directly — tempo, pitches,
durations, and velocities all round-trip. Hand-edit `twinkle.ss` before the
`run` step to change exactly what you edited and nothing else.

`--emit-ss` and `--append` cannot be combined: `--append` never keeps a
single AST representing "existing script + composed track" (it merges the
composed track into an already-interpreted program), so there's no correct
program to print. Passing both flags together is a usage error.

See [whats-new-v6.md](whats-new-v6.md) for the full design rationale.

### Determinism

Identical text produces identical MIDI bytes on every platform:

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" a.mid
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" b.mid
sha256sum a.mid b.mid   # identical hashes
```

## `prosody` — word-level text to melody (V5)

```
soundscript prosody "<text>" [output.mid] [--append <script.ss>] [--emit-ss <path.ss>]
```

Same shape as `compose`, but pitch is planned top-down (phrase → word →
syllable) by the [`ProsodyComposer`](word-prosody.md) instead of per phoneme
category — the melody follows spoken stress and sentence contour rather than
a fixed pitch per phoneme. `compose`/`PhonemeComposer` are unaffected;
`prosody` is a separate, independent verb.

### Standalone

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star"
```

```
Composed 7 syllable(s) into 24 note(s) to output.mid at 96 BPM.
```

### `--append` — add the composed track to a script

Same behavior as `compose --append`, but appends a `prosody` track instead of
a `phonemes` track:

```bash
dotnet run --project src/SoundScript.Cli -- prosody "How I wonder what you are" out.mid --append examples/vocal-song.ss
```

### `--emit-ss` — export the composed AST as `.ss` source (V6)

Identical in shape and guarantees to `compose --emit-ss` (see above), just
sourced from the `ProsodyComposer`'s AST instead:

```bash
dotnet run --project src/SoundScript.Cli -- prosody "Twinkle twinkle little star" --emit-ss twinkle-prosody.ss
dotnet run --project src/SoundScript.Cli -- run twinkle-prosody.ss twinkle-prosody-viass.mid
```

Also mutually exclusive with `--append`, for the same reason.

## `render` — MIDI to audio (V4)

```
soundscript render <file.mid> --css <style.ssc> --out <output.wav|ogg> [--text "<source text>"]
```

Offline, deterministic timbre synthesis using [SoundCSS](soundcss.md):

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" twinkle.mid
dotnet run --project src/SoundScript.Cli -- render twinkle.mid \
  --css examples/default.ssc --out twinkle.wav \
  --text "Twinkle twinkle little star"
```

```
Rendered twinkle.mid with examples/default.ssc to twinkle.wav.
```

| Flag | Required | Purpose |
|------|----------|---------|
| `--css` | yes | SoundCSS (`.ssc`) stylesheet path |
| `--out` | no | Output path (default: `output.wav` in cwd) |
| `--text` | no | Source text for phoneme → MIDI note alignment |

MIDI is never modified — the renderer **reads** note events and adds spectral
envelopes via the [timbre engine](timbre-engine.md).

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | MIDI written successfully |
| `1` | Usage error, missing file, empty compose text, or compile error (message on stderr) |

## Related

- [text-to-melody.md](text-to-melody.md) — how `compose`/`prosody` work, including the `--emit-ss` detour (V6)
- [phoneme-composer.md](phoneme-composer.md) — module reference
- [word-prosody.md](word-prosody.md) — `ProsodyComposer` module reference (V5)
- [whats-new-v6.md](whats-new-v6.md) — `--emit-ss` design and guarantees (V6)
- [soundcss.md](soundcss.md) — SoundCSS timbre language (V4)
- [timbre-engine.md](timbre-engine.md) — offline renderer (V4)
- [language-reference.md](language-reference.md) — script syntax for `run`
- [examples.md](examples.md) — example catalog
