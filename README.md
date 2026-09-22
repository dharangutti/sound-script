# SoundScript

Write audio and media like code — and turn suitable audio back into editable
SoundScript.

[![Tests](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml/badge.svg)](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

SoundScript is a deterministic programming language and .NET toolkit for
programmable audio and media. Its `.ss`, `.ssw`, and `.ssv` scripts compile
to reproducible MIDI, WAV, and WebM outputs. V13 also provides experimental
audio-to-code transcription for suitable recordings. The cross-platform
`soundscript` CLI validates and inspects source, renders audio, and exports
temporal media.

[Try the Playground](https://soundscript.net/playground/) · [Quick start](docs/quick-start.md) · [NuGet guide](docs/nuget.md) · [CLI reference](docs/cli.md) · [Contributing](CONTRIBUTING.md)

## See it work

### Transcribe a melody

Import WAV, MP3 or video audio into editable SoundScript with the additive
[Transcription subsystem](docs/transcription.md). The Playground source includes
a dedicated **Transcription** tab for local upload, analysis, editing and playback.

```powershell
dotnet run --project src/SoundScript.Cli -- transcribe melody.mp4 --out melody.ss --report analysis.json --preview preview.wav
```

Compressed desktop formats require FFmpeg. Monophonic transcription is the
supported baseline; the other four modes are explicitly experimental. See the
[measured accuracy and limitations](docs/transcription-engineering-report.md).
Opt-in [experimental melody extraction](docs/melody-extraction.md) is available via
`--mode extract-melody` and the Playground mode selector. It preserves only one
dominant line and conservatively rejects ambiguous material.
For simultaneous piano notes, use `--mode polyphonic` or **Polyphonic / Piano
(Experimental)**; see [measurements and limitations](docs/polyphonic-transcription.md).
Mixed Audio / Roles estimates symbolic roles rather than isolated stems, and
Percussion / Rhythm emits unpitched `hit` events. See the
[transcription guide](docs/transcription.md) for all five modes.

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
- **Two-way workflow:** author source into audio/media, or transcribe suitable audio into editable SoundScript.
- **Three developer surfaces:** use the CLI for automation, the NuGet package for in-process .NET integration, and the Playground for no-install exploration.

SoundScript is not a DAW, an AI music generator, or merely a MIDI library. It
does not replace live recording, arranging by ear, or generative composition;
it gives developers an inspectable, version-controlled language for defining
the musical and media behavior they want to render.

SoundScript is an independent open-source project. The `SoundScript` 13.0.2
library for net10.0 is [published on NuGet](https://www.nuget.org/packages/SoundScript/13.0.2).
The CLI is distributed separately through [V13 release downloads](https://github.com/dharangutti/sound-script/releases/tag/v13.0.0).

## Musical and media capabilities

V13 includes media-to-SoundScript transcription while retaining the existing
deterministic, backward-compatible authoring workflow. Five modes are available:
Monophonic, Extract Melody (Experimental), Polyphonic / Piano (Experimental),
Mixed Audio / Roles (Experimental), and Percussion / Rhythm (Experimental).
Highlights include:

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

The CLI is available from a source checkout:

```bash
dotnet build SoundScript.sln
dotnet run --project src/SoundScript.Cli -- --version
```

For the CLI, download the archive for your platform from the
[V13 release](https://github.com/dharangutti/sound-script/releases/tag/v13.0.0),
verify its SHA-256 checksum, extract it, and run `soundscript` from that directory.
`SoundScript.Cli` is not published on nuget.org; a locally packed tool can also
be installed as described in the [release checklist](docs/releasing.md).

For the .NET library, install the published package:

```bash
dotnet add package SoundScript --version 13.0.2
```

Local library packing and consumer validation are described in the [NuGet guide](docs/nuget.md).
See the [release checklist](docs/releasing.md) for package inspection and
publishing safeguards.

The current release identity is `13.0.2` (V13, Media-to-SoundScript Transcription).
[Directory.Build.props](Directory.Build.props) is the version source of truth;
release history is in [RELEASE_NOTES.md](RELEASE_NOTES.md).

## Supported platforms

- CLI: Windows, macOS, and Linux with .NET 10.
- Playground: current Chrome, Edge, Firefox, and Safari on desktop and mobile.
- CLI release workflow: Windows x64, Linux x64, macOS x64, and macOS arm64.

## Documentation

- [Documentation hub](docs/documentation.md) — choose a path by goal
- [Quick start](docs/quick-start.md) — Playground, CLI, package, and first script
- [Common tasks](docs/common-tasks.md) — WAV, MIDI, media sync, vocals, and transcription
- [.NET API guide](docs/dotnet-api.md) — compile, render, and transcribe from C#
- [NuGet guide](docs/nuget.md) — local package use and release status
- [NuGet end to end](docs/nuget-end-to-end.md) — telemetry to MIDI/WAV/WebM, media round trip, and verified API parity
- [Application samples](docs/application-samples.md) — DynamicAudio, DevOpsSonification, and TestFixtureGenerator
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
