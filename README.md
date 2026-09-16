# SoundScript

Write deterministic music and media as code.

[![Tests](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml/badge.svg)](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

SoundScript is a programming language for writing deterministic music and
temporal media as plain text. Its `.ss`, `.ssw`, and `.ssv` scripts compile to
reproducible MIDI, WAV, and WebM outputs. The cross-platform `soundscript` CLI
validates and inspects source, renders audio, and exports temporal media.

[Try the Playground](https://soundscript.net/playground/) · [CLI reference](docs/cli.md) · [Contributing](CONTRIBUTING.md)

## See it work

### Transcribe a melody

Import WAV, MP3 or video audio into editable SoundScript with the additive
[Transcription subsystem](docs/transcription.md). The Playground source includes
a dedicated **Transcription** tab for local upload, analysis, editing and playback.

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe melody.mp4 --out melody.ss --report analysis.json --preview preview.wav
```

Compressed desktop formats require FFmpeg. The first release supports solo melodies;
see the [measured accuracy and limitations](docs/transcription-engineering-report.md).
Opt-in [experimental melody extraction](docs/melody-extraction.md) is available via
`--mode extract-melody` and the Playground mode selector. It preserves only one
dominant line and conservatively rejects ambiguous material.
For simultaneous piano notes, use `--mode polyphonic` or **Polyphonic / Piano
(Experimental)**; see [measurements and limitations](docs/polyphonic-transcription.md).

The repository includes a temporal audio/visual composition with source, a
browser demo, and a decode-verified WebM export:

| Source | Result |
| --- | --- |
| [visual-temporal.ssv](examples/visual-temporal.ssv) | [visual-temporal.webm](docs/assets/demos/visual-temporal.webm) |
| [piano.ss](docs/assets/demos/piano.ss) | [piano.wav](docs/assets/demos/piano.wav) · [piano.mid](docs/assets/demos/piano.mid) |

For a browser-first experiment, open the [Playground](https://soundscript.net/playground/).
For automation, CI, and version-controlled assets, use the CLI.

## Try it in 60 seconds

No installation: open the [Playground](https://soundscript.net/playground/) to
edit and run a script in the browser.

For the CLI, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0),
then run a deterministic WAV example from a checkout:

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet run --project src/SoundScript.Cli -- wave examples/full-song-wave.ss --out song.wav
```

That command writes a deterministic WAV file without a DAW, plugin, account,
or server. To inspect a source file before exporting it:

```bash
dotnet run --project src/SoundScript.Cli -- validate examples/visual-temporal.ssv
dotnet run --project src/SoundScript.Cli -- inspect examples/visual-temporal.ssv --at 1.5
```

The first command prints structured diagnostics; the second prints the visual
state at exactly 1.5 seconds.

## Why SoundScript?

- **Versionable:** compositions and media timelines are plain text, so they work with Git and code review.
- **Deterministic:** identical source produces identical MIDI and WAV bytes; use hashes in CI when reproducibility matters.
- **One CLI:** validate, inspect, compose text to melody, render audio, and export WebM without changing tools.
- **Standard outputs:** generated MIDI, WAV, OGG, and WebM files can move into existing audio and media workflows.
- **Two useful surfaces:** the CLI is the automation and production path; the Playground is the no-install learning and experimentation path.

SoundScript is not a DAW, an AI music generator, or merely a MIDI library. It
does not replace live recording, arranging by ear, or generative composition;
it gives developers an inspectable, version-controlled language for defining
the musical and media behavior they want to render.

SoundScript is an independent open-source project. The engine is active and
cross-platform, while the public release and package distribution process is
still maturing.

## Musical and media capabilities

V13 adds monophonic media transcription while retaining the existing deterministic,
backward-compatible authoring workflow. Highlights include:

- Media-to-SoundScript transcription, a canonical musical model, round-trip
  validation, and a dedicated Playground Transcription tab

- MIDI pitches `0–127` (`C-1`–`G9`) and all 128 General MIDI programs
- Extended harmony, dotted notes, triplets and tuplets, grace notes, and
  dynamics from `ppp` through `fff`
- Multiple tracks and layers, plus deterministic MIDI and WAV rendering
- Wave and SoundCSS synthesis, `voice`/`speak`, and temporal visual timelines
  with deterministic WebM export

See [musical-completeness.md](docs/musical-completeness.md) for the precise
supported surface and the intentionally deferred MIDI controls, percussion, and
transposition work.

## CLI at a glance

| Command | Use it for |
| --- | --- |
| `validate` | Parse the complete import graph and report stable diagnostics |
| `inspect` | Read tempo, duration, tracks, notes, visuals, and sync points |
| `run` | Compile `.ss` scripts to MIDI |
| `compose` / `prosody` | Turn text into deterministic MIDI or WAV |
| `render` | Render MIDI through a SoundCSS stylesheet to WAV/OGG |
| `wave` | Render `.ss` / `.ssw` directly to WAV |
| `video` | Export a synchronized WebM through FFmpeg |
| `vocal` | Generate or batch offline vocal stems |

Use `--json` with validation and inspection commands for CI. Exit codes are
stable: `0` success, `1` source error, `2` usage error, `3` missing dependency,
and `4` render/export failure.

### A first script

`examples/blocks.ss` is intentionally self-contained and runs from a fresh
checkout:

```text
block verse {
    mf
    C4 q
    E4 q
    G4 q
}

track melody {
    instrument piano
    play verse
}
```

```bash
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss --out melody.mid
```

### Text to melody

```bash
dotnet run --project src/SoundScript.Cli -- compose \
  "Twinkle twinkle little star" twinkle.wav --wave
```

The same text and options produce the same output bytes. See
[text-to-melody.md](docs/text-to-melody.md) for the composition model.

### Temporal media

```bash
dotnet run --project src/SoundScript.Cli -- video \
  examples/visual-temporal.ssv --out scene.webm --fps 30
```

WebM export requires FFmpeg with `libvpx-vp9` and `libopus`. Install it with
your platform package manager, set `SOUNDSCRIPT_FFMPEG`, or pass `--ffmpeg`.
Use `video --check` to preflight an export without writing media.

## Installation

The source checkout is the supported installation path today:

```bash
dotnet build SoundScript.sln
dotnet run --project src/SoundScript.Cli -- --version
```

`SoundScript.Cli` is configured and locally verifiable as a .NET tool package,
but it is not yet published to NuGet. After publication, the intended global
installation is:

```bash
dotnet tool install --global SoundScript.Cli
soundscript --version
```

Until then, build from source, install a locally generated package as described
in [the release checklist](docs/releasing.md), or use a reviewed release archive
when one is available on the [Releases page](https://github.com/dharangutti/sound-script/releases).

The current release identity is `13.0.0` (V13, Media-to-SoundScript Transcription).
[Directory.Build.props](Directory.Build.props) is the version source of truth;
release history is in [RELEASE_NOTES.md](RELEASE_NOTES.md).

## Supported platforms

- CLI: Windows, macOS, and Linux with .NET 10.
- Playground: current Chrome, Edge, Firefox, and Safari on desktop and mobile.
- CLI release workflow: Windows x64, Linux x64, macOS x64, and macOS arm64.

## Documentation

- [User guide](docs/user-guide.md) — hands-on introduction
- [Language reference](docs/language-reference.md) — complete syntax
- [CLI reference](docs/cli.md) — commands, JSON schema, exit codes, and FFmpeg
- [Examples](docs/examples.md) — catalog of runnable scripts
- [Wave grammar](docs/wave-grammar.md) — direct audio authoring
- [Visual timeline](docs/visual-temporal.md) — temporal media semantics
- [Vocal and phonetics](docs/vocal.md) — lyrics and vocal tracks
- [SoundCSS](docs/soundcss.md) — timbre stylesheets and offline synthesis
- [Architecture](docs/architecture.md) — source layout and interpreter pipeline
- [Playground guide](docs/PLAYGROUND.md) — browser workflow and presets
- [Audio/visual compositions](docs/audio-visual-compositions.md) — practical showcases

## Contributing

Start with [CONTRIBUTING.md](CONTRIBUTING.md). It covers the wordbank
submodule, build and test commands, CLI smoke tests, and the shape of a useful
pull request. Use the issue templates for bugs and ideas, and include the
source file, command, operating system, and observed output in reports.

If SoundScript helps you create reproducible music or media, consider starring
the repository. If you build something with it, share the use case in a
[Discussion](https://github.com/dharangutti/sound-script/discussions) or open a
focused issue.

## License

SoundScript is released under the [MIT License](LICENSE).

Mixed recordings can use `--mode mixed --roles melody,bass`; these are [symbolic musical-role estimates](docs/mixed-audio-transcription.md), not isolated stems.

Percussion loops use `--mode percussion` or **Percussion / Rhythm**. See [unpitched hit syntax, measurements and limitations](docs/percussion-transcription.md).
