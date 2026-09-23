# Common tasks

## Generate a WAV

Use the CLI's direct Wave backend for .ss and .ssw source:

~~~bash
dotnet run --project src/SoundScript.Cli -- wave examples/wave-effects.ssw --out effects.wav
~~~

Add --stereo for stereo output. From C#, compile in memory and save the
returned RIFF/WAVE bytes with SoundScriptCompilation.RenderWave(); see the
[.NET API guide](dotnet-api.md).

## Generate MIDI

~~~bash
dotnet run --project src/SoundScript.Cli -- run examples/blocks.ss --out blocks.mid
~~~

The .NET equivalent is SoundScriptCompilation.RenderMidi(). MIDI rendering
uses the existing pitched-event interpreter. A hit percussion event is
intentionally rejected by the MIDI backend until a dedicated mapping exists;
render percussion scripts through Wave instead.

## Create synchronized audio and visuals

Use .ssv source with sync audio, visual blocks, and a musical track:

~~~bash
dotnet run --project src/SoundScript.Cli -- video examples/visual-temporal.ssv --out scene.webm --fps 30
~~~

The CLI video export requires FFmpeg with VP9 and Opus support. The Playground
can preview and export the same temporal model in a browser.

The NuGet package also exposes the temporal scene, audio, frame and FFmpeg
export APIs. See the [Industrial Monitoring tutorial](tutorials/industrial-monitoring.md)
for an application that changes synchronized media from typed telemetry values.

## Add vocals or speech cues

Use voice, vocal, and speak syntax where supported by the selected backend. The
Wave backend can render synthetic prosody or mix pre-rendered stems. See
[Vocal](vocal.md) and [Wave grammar](wave-grammar.md) for supported forms and
[CLI](cli.md) for vocal batch and --offline-tts.

## Transcribe suitable audio

The CLI accepts WAV and, with FFmpeg, common desktop media formats:

~~~bash
dotnet run --project src/SoundScript.Cli -- transcribe melody.wav --out melody.ss --report melody.json --preview melody-preview.wav
~~~

The analysis limit is 120 seconds. Use --start and --duration for an excerpt.
Choose one of five modes deliberately: Monophonic, Extract Melody
(Experimental), Polyphonic / Piano (Experimental), Mixed Audio / Roles
(Experimental), or Percussion / Rhythm (Experimental). Read the
[transcription guide](transcription.md) before interpreting confidence or
suitability.

## Edit generated SoundScript

Generated source is ordinary .ss text. Change pitches, rests, durations, tempo,
instruments, or dynamics, then validate and render it again:

~~~bash
dotnet run --project src/SoundScript.Cli -- validate melody.ss
dotnet run --project src/SoundScript.Cli -- wave melody.ss --out edited.wav
~~~

For transcription, the source writer preserves accepted notes and percussion
hits where the current output format supports them. Generated playback is a
consistency check; it is not proof that the source recording was transcribed
perfectly.

## Generate deterministic fixtures

Keep a short script beside a test and render it during setup. Identical source,
options, assets, and renderer version produce identical output bytes. Hash the
result if a test needs an explicit reproducibility assertion. See the
[Test Fixture Generator sample](application-samples.md#testfixturegenerator).
