# CLI Reference

The SoundScript CLI (`SoundScript.Cli`, assembly name `soundscript`) compiles
scripts and text to MIDI, renders offline timbre audio, and renders `.ss`/`.ssw`
scripts directly to WAV. It requires the .NET 8 SDK and runs on Windows, macOS,
and Linux.

```bash
dotnet run --project src/SoundScript.Cli -- <verb> <arguments>
```

Print the version and exit:

```bash
dotnet run --project src/SoundScript.Cli -- --version
dotnet run --project src/SoundScript.Cli -- -v
```

## Verbs

| Verb | Purpose |
|------|---------|
| `run` | Compile a `.ss` script to MIDI |
| `compose` | Compose plain text to MIDI via the [PhonemeComposer](phoneme-composer.md) (V3.1) |
| `prosody` | Compose plain text to MIDI via the word-level [ProsodyComposer](word-prosody.md) (V5) |
| `render` | Offline timbre synthesis: MIDI + SoundCSS → WAV/OGG ([V4](whats-new-v4.md)) |
| `wave` | Direct wave synthesis: `.ss`/`.ssw` → WAV via [SoundScript.Wave](wave-grammar.md) ([V7](whats-new-v7.md)) |

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
soundscript compose "<text>" [output.mid|output.wav] [--append <script.ss>] [--emit-ss <path.ss>] [--wave] [--stereo]
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

### `--wave` — compose directly to WAV (V7)

Skips the MIDI step and renders the composed AST through
[SoundScript.Wave](wave-grammar.md):

```bash
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" twinkle.wav --wave
dotnet run --project src/SoundScript.Cli -- compose "Twinkle twinkle little star" twinkle.wav --wave --emit-ss twinkle.ss
```

```
Composed 7 syllable(s) into 24 note(s) and rendered to twinkle.wav via SoundScript.Wave (no MIDI step) at 96 BPM.
```

| Flag | Purpose |
|------|---------|
| `--wave` | Render through SoundScript.Wave instead of writing MIDI |
| `--stereo` | Write stereo WAV (with `--wave` only) |

`--wave` cannot be combined with `--append`. It can be combined with
`--emit-ss` to export both `.ss` source and a `.wav` in one command.

## `prosody` — word-level text to melody (V5)

```
soundscript prosody "<text>" [output.mid|output.wav] [--append <script.ss>] [--emit-ss <path.ss>] [--wave] [--stereo]
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

`--wave` and `--stereo` behave the same as on `compose` — see
[`compose --wave`](#--wave--compose-directly-to-wav-v7).

## `render` — MIDI to audio (V4)

```
soundscript render <file.mid> --css <style.ssc> [--out <output.wav|ogg>] [--text "<source text>"]
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

## `wave` — script to WAV (V8)

```
soundscript wave <script.ss|script.ssw> [output.wav] [--stereo]
  [--vocal <stem.wav>] [--vocal-at=<beats>] [--vocal-gain=<0-1>]
  [--tts-dir <folder>]
  [--offline-tts [espeak|prosody]] [--offline-tts-dir <folder>] [--offline-tts-voice <id>] [--seed=<n>]
```

Renders a script directly through [SoundScript.Wave](wave-grammar.md) with no
MIDI step. V8 adds **vocal stem mixing** — your own recordings in the exported
WAV (see [whats-new-v8.md](whats-new-v8.md)).

```bash
dotnet run --project src/SoundScript.Cli -- wave examples/full-song-wave.ss jingle.wav
dotnet run --project src/SoundScript.Cli -- wave examples/wave-vocal-stem.ssw vocal.wav
dotnet run --project src/SoundScript.Cli -- wave song.ssw out.wav \
  --vocal my-recording.wav --vocal-gain=0.9
dotnet run --project src/SoundScript.Cli -- wave song.ssw out.wav --tts-dir vocal-stems/
dotnet run --project src/SoundScript.Cli -- wave song.ssw out.wav \
  --offline-tts prosody --offline-tts-dir vocal-stems/
```

| Flag / argument | Required | Purpose |
|-----------------|----------|---------|
| `script.ss\|script.ssw` | yes | Input script path |
| `output.wav` | no | Output path (default: `output.wav` in cwd) |
| `--stereo` | no | Write stereo WAV instead of mono |
| `--vocal` | no | Mix a full vocal stem WAV (path relative to cwd or absolute) |
| `--vocal-at` | no | Start beat for `--vocal` (default: `0`) |
| `--vocal-gain` | no | Linear gain for `--vocal` (default: `1.0`) |
| `--tts-dir` | no | Folder of pre-rendered WAVs mapped to each `speak` phrase (slug filenames) |
| `--offline-tts` | no | Generate slug-named stems with an offline engine, then mix (default engine: espeak if installed, else prosody) |
| `--offline-tts-dir` | no | Output folder for `--offline-tts` (default: `<script-dir>/vocal-stems`) |
| `--offline-tts-voice` | no | eSpeak voice id when using `--offline-tts` (default: `en`) |
| `--seed` | no | Prosody seed for synthetic stems (default: `7`) |

**Flag ordering:** place `[output.wav]` immediately after the script path, before
`--stereo`. `wave script.ss --stereo out.wav` ignores the custom path and uses
the default `output.wav`.

Unlike `run`, the wave verb does not invoke `VocalInterpreter` — it loads the
AST via `ProgramLoader` and renders through `WaveRenderer` only. Wave-only
directives (`effect`, `speak`, named `humanize`) are rejected by `run` with a
clear error; use `wave` (or the Playground's automatic wave routing) instead.

## `vocal` — offline stem generation (V8)

```
soundscript vocal generate "<text>" --out <file.wav> [--wordbank-dir <path>] [--engine wordbank|composite|espeak|prosody] [--locale <code>] [--voice <id>] [--seed=<n>]
soundscript vocal batch <script.ss|script.ssw> --out-dir <folder> [--wordbank-dir <path>] [--engine wordbank|composite|espeak|prosody] [--locale <code>] [--voice <id>] [--seed=<n>] [--skip-existing]
```

Generates slug-named WAV files for `speak` phrases without rendering the full
mix. Uses the **SoundScript.Vocal** library (same engines as `wave --offline-tts`).

```bash
# Default: composite (corpus human audio → G2P timbre → espeak → prosody per word)
dotnet run --project src/SoundScript.Cli -- vocal generate "Hello welcome" \
  --out vocal-stems/hello-welcome.wav --locale en

dotnet run --project src/SoundScript.Cli -- vocal batch song.ssw \
  --out-dir vocal-stems/ --engine composite --skip-existing
```

| Flag / argument | Required | Purpose |
|-----------------|----------|---------|
| `generate` / `batch` | yes | Subcommand |
| `"<text>"` or `script.ssw` | yes | Phrase or script containing `speak` nodes |
| `--out` / `--out-dir` | yes | Output WAV path or folder |
| `--wordbank-dir` | no | Load locale packs + corpus from a wordbank checkout (`WORDBANK_DIR` env also works) |
| `--engine` | no | `composite` (default), `wordbank`, `espeak`, or `prosody` |
| `--locale` | no | Wordbank locale for corpus lookup and G2P (default: active catalog locale) |
| `--voice` | no | eSpeak voice id (default: `en`) |
| `--seed` | no | Prosody seed (default: `7`) |
| `--skip-existing` | no | Batch only — skip files that already exist |

→ [phase8-wordbank-vocal.md](phase8-wordbank-vocal.md) for corpus audio, licenses, and examples.

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Output written successfully (MIDI, WAV, or OGG) |
| `1` | Usage error, missing file, empty compose text, or compile/render error (message on stderr) |

## Related

- [text-to-melody.md](text-to-melody.md) — how `compose`/`prosody` work, including the `--emit-ss` detour (V6)
- [phoneme-composer.md](phoneme-composer.md) — module reference
- [word-prosody.md](word-prosody.md) — `ProsodyComposer` module reference (V5)
- [whats-new-v6.md](whats-new-v6.md) — `--emit-ss` design and guarantees (V6)
- [whats-new-v7.md](whats-new-v7.md) — SoundScript.Wave and the `wave` verb (V7)
- [whats-new-v8.md](whats-new-v8.md) — vocal stems and `vocal` CLI (V8)
- [wave-grammar.md](wave-grammar.md) — `.ssw` grammar reference (V7+)
- [soundcss.md](soundcss.md) — SoundCSS timbre language (V4)
- [timbre-engine.md](timbre-engine.md) — offline renderer (V4)
- [language-reference.md](language-reference.md) — script syntax for `run`
- [examples.md](examples.md) — example catalog
