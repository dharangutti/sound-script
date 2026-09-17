# SoundScript

SoundScript is a deterministic programming language and .NET toolkit for programmable
audio and media: MIDI, WAV, vocals, synchronized visuals, and experimental audio-to-code
transcription. Use it for application cues, sonification, or reproducible media fixtures.

## Install

Requires .NET 10. Once this release is published to nuget.org:

```sh
dotnet add package SoundScript --version 13.0.0
```

Before publication, [build and install from a local feed](https://soundscript.net/doc.html?p=nuget.md).

## Five-minute example

```csharp
using SoundScript;

var cue = SoundScriptEngine.Compile("tempo 120 track cue { instrument piano mf C4 e E4 e G4 q }");
File.WriteAllBytes("success.wav", cue.RenderWave());
File.WriteAllBytes("success.mid", cue.RenderMidi());
```

## Programmatic rendering

```csharp
var compilation = SoundScriptEngine.CompileFile("notification.ss");
foreach (var warning in compilation.Warnings) Console.WriteLine(warning);
File.WriteAllBytes("notification.wav", compilation.RenderStereoWave());
```

`CompileFile` resolves imports and relative sample paths. In-memory `Compile` rejects
imports. Backend validation runs when rendering; MIDI explicitly rejects unpitched
`hit` events. Identical source, assets, options, and engine version produce repeatable
audio; compressed-media decoding and experimental transcription have separate limits.

## Transcription

```csharp
using SoundScript.Transcription;

var audio = PcmWaveInput.Decode(File.ReadAllBytes("melody.wav"));
var result = await new TranscriptionEngine().TranscribeAsync(audio);
Console.WriteLine(result.Suitability);
var source = new SoundScriptOutput().Source(result.Score);
File.WriteAllText("melody.ss", source);
```

Start with a short, clean solo melody. Review suitability and diagnostics before using
the result. Melody extraction, piano polyphony, mixed roles, and percussion are
experimental. Mixed roles are symbolic estimates, not isolated stems; reconstruction
consistency is not ground-truth accuracy. Compressed desktop input and WebM export
require a separate FFmpeg installation.

## CLI relationship and links

This library complements the existing `soundscript` CLI; it neither installs nor
changes CLI commands. The package contains the reusable libraries, XML
IntelliSense documentation for the bundled public assemblies, and the vocal
corpus, without CLI or Playground binaries.

- [Quick start](https://soundscript.net/doc.html?p=quick-start.md)
- [.NET API guide](https://soundscript.net/doc.html?p=dotnet-api.md)
- [Full documentation](https://soundscript.net/doc.html?p=documentation.md)
- [Application samples](https://github.com/dharangutti/sound-script/tree/main/samples)
- [Transcription limits](https://soundscript.net/doc.html?p=transcription.md)
- [Playground](https://soundscript.net/playground/)
- [GitHub](https://github.com/dharangutti/sound-script)
- [Project website](https://soundscript.net/)

Engine: MIT. Included wordbank corpus: CC0. Font license notices are in `licenses/`.
