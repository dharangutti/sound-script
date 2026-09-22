# Application samples

The application samples are small .NET console projects that consume the
SoundScript package through its facade and public component APIs. They demonstrate integration patterns rather than new
language features or a new audio engine. Each sample uses the same source and
render semantics as the CLI.

## Complete NuGet workflows

- [IndustrialMonitoring](../samples/IndustrialMonitoring): one typed scenario
  builder maps Healthy, Warning and Critical telemetry to `.ss`, `.ssw` and
  `.ssv`, MIDI, mono/stereo WAV, temporal states and optional decode-verified WebM.
- [MediaRoundTrip](../samples/MediaRoundTrip): original PCM fixture → structured
  transcription → editable source → instrument change → MIDI and WAV.

Both use `PackageReference` to published SoundScript 13.0.2. Start with the
[end-to-end guide](nuget-end-to-end.md),
[monitoring tutorial](tutorials/industrial-monitoring.md) or
[round-trip tutorial](tutorials/media-round-trip.md).

## DynamicAudio

Models application events such as welcome, success, warning, new message, and
failure. The event handler chooses a short SoundScript cue or varies supported
musical properties, then renders the selected source through SoundScriptEngine.
This proves that application state can drive versioned, deterministic audio at
runtime.

## DevOpsSonification

Maps a build or test summary to repeatable cues. For example, a fully passing
run can use a success motif, an unstable run a warning rhythm, and failures a
lower or denser cue. The same counts and status produce the same generated
bytes, making the sample useful for CI demonstrations and human-readable build
signals.

## TestFixtureGenerator

Generates exact WAV and MIDI fixtures from short source strings during test or
demo setup. A consumer can hash the bytes, store them as a fixture, and compare
future output for regressions. The sample makes the pipeline explicit:

~~~text
C# setup → SoundScript source → deterministic WAV/MIDI fixture → consumer test
~~~

## Running samples

The sample projects live under samples/ as DynamicAudio, DevOpsSonification,
and TestFixtureGenerator:

~~~bash
dotnet run --project samples/DynamicAudio/DynamicAudio.csproj
dotnet run --project samples/DevOpsSonification/DevOpsSonification.csproj -- --total 100 --passed 96 --failed 4
dotnet run --project samples/TestFixtureGenerator/TestFixtureGenerator.csproj
~~~

Each sample prints its inputs, output path, and the relevant
determinism/application behavior. The samples are intentionally small and do
not duplicate SoundScript internals. Each sample directory also contains a
short README with its output files and options.

For existing language examples and ready-to-run scripts, see
[examples.md](examples.md). For sonification case studies, see
[use-cases.md](use-cases.md) and the [Industrial Audio site](https://soundscript.net/industrial/).
