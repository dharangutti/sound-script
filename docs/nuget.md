# SoundScript NuGet package

SoundScript is the single bundled package for programmatic .NET use. The package targets net10.0 and includes the reusable parser, core, MIDI, Wave,
media, vocal, prosody, voice, and transcription assemblies. The CLI remains a
separate application surface.

The [programmable media runtime](programmatic-media-runtime.md) provides synchronized audio and queryable visual state.

## Install

<!-- GENERATED:DOTNET_REQUIREMENT_START -->
Requires .NET 10.0 (`net10.0`). Use the SDK selected by `global.json` for repository development.
<!-- GENERATED:DOTNET_REQUIREMENT_END -->

<!-- GENERATED:LIBRARY_INSTALL_START -->
Install the published library:

```bash
dotnet add package SoundScript --version 14.0.0
```

[SoundScript 14.0.0 on NuGet](https://www.nuget.org/packages/SoundScript/14.0.0).
<!-- GENERATED:LIBRARY_INSTALL_END -->

For local development validation, pack and use a local source:

<!-- GENERATED:LOCAL_LIBRARY_INSTALL_START -->
```powershell
dotnet pack src/SoundScript/SoundScript.csproj -c Release --output artifacts/nuget
dotnet new console -n PackageConsumer -f net10.0
dotnet add PackageConsumer/PackageConsumer.csproj package SoundScript --version 15.0.0 --source artifacts/nuget
dotnet run --project PackageConsumer/PackageConsumer.csproj
```
<!-- GENERATED:LOCAL_LIBRARY_INSTALL_END -->

Run the consumer outside the repository solution when checking package
contents. A local package source verifies the nupkg without publishing it.

## First use

~~~csharp
using SoundScript;

var cue = SoundScriptEngine.Compile(
    "tempo 120 track cue { instrument piano mf C4 e E4 e G4 q }");
File.WriteAllBytes("success.wav", cue.RenderWave());
File.WriteAllBytes("success.mid", cue.RenderMidi());
~~~

See the [full .NET API guide](dotnet-api.md) for imports, stereo rendering,
Wave options, transcription, and expected errors.

For complete package consumers, see [NuGet end to end](nuget-end-to-end.md):
typed monitoring scenarios generate MIDI, Wave audio and temporal A/V; a second
application transcribes WAV, edits the score and regenerates MIDI/WAV.

## Package metadata

The package project records the SoundScript ID, inherited development version, .NET 10 target,
project and repository URLs, MIT license metadata, discoverability tags, XML
documentation for the public facade and bundled public APIs, README, icon, and
Source Link metadata. Final package inspection must verify XML files for all
bundled public assemblies in the nupkg; PDB files belong in the companion
symbols package (snupkg). The package README is
packaging/README.md and is kept concise for Visual Studio and nuget.org
discovery; this documentation site contains the longer guides.

## What the package does not do

The package does not publish itself, install the CLI as a global tool, provide a
microphone/live transcription service, or promise universal transcription. The
five transcription modes are bounded offline analyses. Review suitability and
mode-specific evidence before using generated source as a factual record of a
recording.
