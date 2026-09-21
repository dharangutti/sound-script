# .NET API guide

The SoundScript package targets net10.0 and presents a small facade over the
existing parser and renderers. It is one distribution: the package project
bundles the reusable SoundScript assemblies, including transcription, instead
of asking an application to reference the CLI.

## Compile and render in memory

~~~csharp
using SoundScript;

var compilation = SoundScriptEngine.Compile("""
    tempo 120
    track cue {
        instrument piano
        mf
        C4 e
        E4 e
        G4 q
    }
    """);

File.WriteAllBytes("cue.wav", compilation.RenderWave());
File.WriteAllBytes("cue.mid", compilation.RenderMidi());
~~~

Compile parses in-memory source and returns SoundScriptCompilation. RenderWave()
returns a complete mono PCM WAV; RenderStereoWave() returns a stereo WAV;
RenderMidi() returns a standard MIDI file. The bytes are ready to write to a
file, HTTP response, object store, or test fixture.

Each WAV method has a parameterless overload and an overload accepting
WaveRenderOptions?. The parameterless form uses the entry-file directory when
available; pass explicit options when selecting sample paths or overlays.

Compilation does not start a CLI process. Syntax errors and parser errors are
reported when compiling. Backend-specific validation happens while rendering,
so a script can compile successfully and still be unsuitable for MIDI.

## Load imports from a file

~~~csharp
var compilation = SoundScriptEngine.CompileFile("scores/notification.ss");
Console.WriteLine($"Loader warnings: {compilation.Warnings.Count}");
File.WriteAllBytes("notification.wav", compilation.RenderWave());
~~~

CompileFile resolves relative imports and sample paths from the entry file's
directory. It reads the local filesystem; load only trusted scripts and assets.
Use Compile for source that contains no imports.

For hosted execution, use `CompileFile(path, new SoundScript.Core.AllowedPathRoot(jobDirectory))`.
The explicit boundary constrains the entry, nested imports and filesystem samples;
render options cannot remove it. The host must prevent concurrent filesystem
mutation and provide separate process/resource isolation. See
[V13 Reliability & Release Hardening](v13-reliability-hardening.md#import-and-resource-root).

## Render options

Pass SoundScript.Wave.WaveRenderOptions to RenderWave or RenderStereoWave when a
script uses relative sample paths or external audio overlays:

~~~csharp
using SoundScript.Wave;

var wav = compilation.RenderWave(new WaveRenderOptions {
    ScriptDirectory = "scores",
    SkipMissingSamples = false
});
~~~

Omitting options uses the entry file directory for CompileFile; explicit options
replace those defaults. Identical source, options, assets, and renderer version
are the inputs to deterministic output.

## Transcription

Transcription is in the bundled SoundScript.Transcription assembly. Decode a
native PCM WAV with PcmWaveInput, then select a mode through TranscriptionEngine:

~~~csharp
using SoundScript.Transcription;

var audio = PcmWaveInput.Decode(File.ReadAllBytes("melody.wav"));
var result = await new TranscriptionEngine().TranscribeAsync(
    audio,
    new TranscriptionOptions(Tempo: 120, Instrument: 73),
    TranscriptionMode.Monophonic);

Console.WriteLine(result.Suitability.Status);
var source = new SoundScriptOutput().Source(result.Score);
File.WriteAllText("melody.ss", source);
~~~

AnalysisAudio is normalized to finite mono PCM at 16 kHz and is limited to 120
seconds. DesktopMediaInput uses FFmpeg for supported WAV/MP3/MP4/M4A, WebM,
Ogg, FLAC, AAC, and MOV files; set SOUNDSCRIPT_FFMPEG or pass the executable
path when needed.

The result exposes score, observations, diagnostics, mode-specific evidence,
and a conservative Suitability gate. Experimental means the output may be
useful for editing or exploration but needs review. Mixed roles are symbolic
estimates, not isolated stems. Percussion produces unpitched hit events.

## CLI relationship and compatibility

The package complements the existing CLI. It does not embed the CLI executable
or change command names, output semantics, or Playground behavior. Use the CLI
for shell automation and FFmpeg export; use the package when a .NET application
needs to compile, render, transcribe, or generate fixtures in process.

## Errors to handle

- ArgumentNullException for null source input.
- NotSupportedException for imports passed to Compile, unsupported output
  features, or unsupported WAV encodings without FFmpeg.
- FileNotFoundException for missing entry scripts, imports, or media files.
- InvalidDataException for malformed PCM, overlong analysis input, or rejected
  transcription data.

The public facade keeps the AST and parser implementation details out of normal
consumer code while retaining the existing component assemblies inside the
single package.
