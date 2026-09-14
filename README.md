# SoundScript

Write deterministic music and media as code.

[![Tests](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml/badge.svg)](https://github.com/dharangutti/sound-script/actions/workflows/tests.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

SoundScript turns plain text and `.ss` / `.ssw` / `.ssv` scripts into reproducible
MIDI, WAV, and WebM files. The cross-platform `soundscript` CLI validates and
inspects source, renders audio, and exports temporal media. The same source is
designed to produce the same result across runs and platforms.

[Try the Playground](https://soundscript.net/playground/) · [CLI reference](docs/cli.md) · [Contributing](CONTRIBUTING.md)

## See it work

The repository includes a temporal audio/visual composition with source, a
browser demo, and a decode-verified WebM export:

| Source | Result |
| --- | --- |
| [visual-temporal.ssv](examples/visual-temporal.ssv) | [visual-temporal.webm](docs/assets/demos/visual-temporal.webm) |
| [piano.ss](docs/assets/demos/piano.ss) | [piano.wav](docs/assets/demos/piano.wav) · [piano.mid](docs/assets/demos/piano.mid) |

For a browser-first experiment, open the [Playground](https://soundscript.net/playground/).
For automation, CI, and version-controlled assets, use the CLI.

## Quick start

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet run --project src/SoundScript.Cli -- wave examples/full-song-wave.ss --out song.wav
```

That command writes a deterministic WAV file without a DAW, plugin, account, or
server. To inspect a source file before exporting it:

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

SoundScript is an independent open-source project. The engine is active and
cross-platform, while the public release and package distribution process is
still maturing.

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

## Installation and releases

The source checkout is the currently supported installation path:

```bash
dotnet build SoundScript.sln
dotnet run --project src/SoundScript.Cli -- --version
```

`SoundScript.Cli` is configured as a .NET tool package, but it is not currently
published to NuGet. Until a public package is available, do not rely on
`dotnet tool install --global SoundScript.Cli`; build from source or use a
published release archive when one is provided on the [Releases page](https://github.com/dharangutti/sound-script/releases).

The current version is `11.1.0` (V11.1, CLI Productization). The version source
is [Directory.Build.props](Directory.Build.props), and release history is in
[RELEASE_NOTES.md](RELEASE_NOTES.md).

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
