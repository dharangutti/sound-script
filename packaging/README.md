# SoundScript

SoundScript is a deterministic programming language and .NET toolkit for programmable
audio and media: MIDI, WAV, vocals, synchronized visuals, and experimental audio-to-code
transcription. Use it for application cues, sonification, or reproducible media fixtures.

## Install

<!-- GENERATED:DOTNET_REQUIREMENT_START -->
Requires .NET 10.0 (`net10.0`). Use the SDK selected by `global.json` for repository development.
<!-- GENERATED:DOTNET_REQUIREMENT_END -->

<!-- GENERATED:LIBRARY_INSTALL_START -->
Install the published library:

```bash
dotnet add package SoundScript --version 15.0.0
```

[SoundScript 15.0.0 on NuGet](https://www.nuget.org/packages/SoundScript/15.0.0).
<!-- GENERATED:LIBRARY_INSTALL_END -->

For development, you can also [build and install from a local feed](https://soundscript.net/doc.html?p=nuget.md).

## Five-minute example

```csharp
using SoundScript;

var cue = SoundScriptEngine.Compile("tempo 120 track cue { instrument piano mf C4 e E4 e G4 q }");
File.WriteAllBytes("success.wav", cue.RenderWave());
File.WriteAllBytes("success.mid", cue.RenderMidi());
```

## Programmatic rendering

V14 adds a typed programmable-media facade over the existing audio and visual engines:

```csharp
using SoundScript.Media;
var media = SoundScriptEngine.Compile("track cue { C4 q } visual \"intro\" for 2s").CompileMedia();
byte[] audio = media.RenderAudio();
var scene = media.SceneAt(TimeSpan.FromSeconds(1));
string json = TemporalVisualJson.Serialize(scene);
string svg = TemporalSvgRenderer.Render(scene);
```

Hosts supply playback time. JSON schema 1.0 and safe SVG consume the existing typed scene;
FFmpeg and browser frameworks are unnecessary. See the
[runtime guide](https://github.com/dharangutti/sound-script/blob/main/docs/programmatic-media-runtime.md).


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
- [End-to-end NuGet samples and API parity](https://soundscript.net/doc.html?p=nuget-end-to-end.md)
- [Full documentation](https://soundscript.net/doc.html?p=documentation.md)
- [Application samples](https://github.com/dharangutti/sound-script/tree/main/samples)
- [Transcription limits](https://soundscript.net/doc.html?p=transcription.md)
- [Playground](https://soundscript.net/playground/)
- [GitHub](https://github.com/dharangutti/sound-script)
- [Project website](https://soundscript.net/)

Engine: MIT. Included wordbank corpus licensing and provenance are recorded per entry
in the corpus metadata bundled with the library and
[`SOURCES.md`](https://github.com/dharangutti/sound-script/blob/main/src/SoundScript.Wordbank/Data/corpus/v2026.07/en/SOURCES.md)
(bundled under `licenses/corpus/v2026.07/en/SOURCES.md` in V16;
V15 packages placed it alongside `contentFiles` corpus data). V16
owns corpus JSON/audio inside assemblies, with no consumer `contentFiles`.
Declared licenses include CC0-1.0 and
CC-BY-4.0; these historical declarations are not verified licensing or provenance.
Of the 66 English pronunciation entries, 61 have declared Commons sources. The five
entries `bobtail`, `dashing`, `sleighing`, `test`, and `world` have unresolved
provenance: their original recording/generation receipts are missing, and their
historical CC0 declarations remain unverified. The wordbank CC0 notice in `licenses/`
does not establish a blanket license for these recordings. Font license notices
are also in `licenses/`.
