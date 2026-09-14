# Contributing to SoundScript

Thanks for helping improve SoundScript. Keep changes focused, explain the user
problem they solve, and include the command or test that verifies the result.

## Prerequisites

- .NET 8 SDK
- Git
- FFmpeg with `libvpx-vp9` and `libopus` for WebM export tests
- Bash for the wordbank synchronization script

Clone the repository and initialize its wordbank submodule:

```bash
git clone https://github.com/dharangutti/sound-script.git
cd sound-script
git submodule update --init --recursive
./scripts/sync-wordbank.sh
```

## Build and test

```bash
dotnet build SoundScript.sln -c Debug
dotnet test src/SoundScript.Tests -c Debug
```

Run a CLI smoke test before opening a pull request:

```bash
dotnet run --project src/SoundScript.Cli -- validate examples/visual-temporal.ssv
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss --out output.mid
```

If you change media rendering, also run the relevant Wave, Visual, or Media
tests and describe the generated-output checksums or inspection output.

## Project layout

- `src/SoundScript.Core`, `Parser`, and `Midi` — language and MIDI pipeline
- `src/SoundScript.Wave`, `Timbre`, and `Vocal` — audio rendering
- `src/SoundScript.Visual` and `Media` — temporal state and export planning
- `src/SoundScript.Cli` — the `soundscript` command line product
- `src/SoundScript.Playground` — browser experimentation surface
- `src/SoundScript.Tests` — unit, integration, and determinism coverage
- `examples` — small runnable programs
- `docs` — user and language documentation

## Pull requests

Open a pull request against `main` with a concise title and a description that
answers:

1. What user problem does this solve?
2. What changed and what remains out of scope?
3. Which build, test, or CLI commands passed?
4. Does the change affect deterministic output or public syntax?

Keep generated build output out of commits. Include source examples and docs
when adding a public command or language feature. For security concerns, do not
open a public issue; contact the repository maintainer privately through GitHub.
