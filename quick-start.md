# Quick start

SoundScript has three entry points: the browser Playground, the cross-platform
CLI, and the SoundScript NuGet package. All three use the same language and
deterministic rendering pipeline.

## Try it in the Playground

Open the [SoundScript Playground](https://soundscript.net/playground/), choose
an example, and run it in the browser. The Playground is client-side and needs
no account or installation. It is the fastest way to hear a script and inspect
the visual timeline.

<!-- GENERATED:CLI_DISTRIBUTION_START -->
`SoundScript.Cli` 16.0.0 is not published on nuget.org.

Download a platform archive from [CLI 16.0.0](https://github.com/dharangutti/sound-script/releases/tag/v16.0.0), verify its SHA-256 checksum, extract it, and run `soundscript` from that directory.
<!-- GENERATED:CLI_DISTRIBUTION_END -->

## Run the CLI from the repository

Install the .NET 10 SDK, clone the repository, and build it:

~~~bash
git clone --recurse-submodules https://github.com/dharangutti/sound-script.git
cd sound-script
dotnet build SoundScript.sln
~~~

If already cloned without submodules, run `git submodule update --init --recursive` from
the repository root before building (see [checkout requirements](../README.md#try-it-in-60-seconds)).

Render a MIDI file and a WAV file:

~~~bash
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss --out melody.mid
dotnet run --project src/SoundScript.Cli -- wave examples/full-song-wave.ss --out song.wav
~~~

validate checks source and its imports; inspect reports timing, tracks, notes,
visuals, and synchronization points:

~~~bash
dotnet run --project src/SoundScript.Cli -- validate examples/blocks.ss
dotnet run --project src/SoundScript.Cli -- inspect examples/visual-temporal.ssv --at 1.5
~~~

## Use the .NET package

<!-- GENERATED:DOTNET_REQUIREMENT_START -->
Requires .NET 10.0 (`net10.0`). Use the SDK selected by `global.json` for repository development.
<!-- GENERATED:DOTNET_REQUIREMENT_END -->

<!-- GENERATED:LIBRARY_INSTALL_START -->
Install the published library:

```bash
dotnet add package SoundScript --version 16.0.0
```

[SoundScript 16.0.0 on NuGet](https://www.nuget.org/packages/SoundScript/16.0.0).
<!-- GENERATED:LIBRARY_INSTALL_END -->

For package installation and API examples, see [NuGet](nuget.md) and the
[.NET API guide](dotnet-api.md).

## First script

Save this as hello.ss:

~~~soundscript
tempo 120
track melody {
    instrument piano
    mf
    C4 q
    E4 q
    G4 h
}
~~~

Then render it with either run (MIDI) or wave (WAV). Source is plain text, so
it can be reviewed, diffed, and reproduced in version control.

## Next steps

- [Common tasks](common-tasks.md) for practical workflows.
- [Language reference](language-reference.md) for complete syntax.
- [Application samples](application-samples.md) for application integration.
