# Media round trip

A .NET 10 consumer of published `SoundScript` **13.0.2**. It generates an
original C4–E4–G4–C5 sine fixture, transcribes native PCM WAV into a structured
score, saves editable source, changes flute to piano, and renders MIDI/WAV.
No FFmpeg or SoundScript CLI is required.

```sh
dotnet build samples/MediaRoundTrip -c Release
dotnet run --project samples/MediaRoundTrip -c Release
dotnet run --project samples/MediaRoundTrip -c Release -- artifacts/my-roundtrip path/to/solo.wav
```

Arguments: `[output-directory] [input.wav]`. Default output is
`artifacts/samples/media-round-trip`. Without an input argument it writes
`Input/melody.wav`. With an argument it reads your WAV without modifying it.
`Output/` contains `report.json`, `transcribed.ss`, `modified.ss`,
`reconstructed.mid`, and `reconstructed.wav`.

The report preserves observations, diagnostics, confidence and suitability.
Generation requires `Suitability.CanGenerate`; rejected audio leaves a report
and returns failure. Prior output files are invalidated first. The fixed
120 BPM transcription option is chosen for the fixture; adapt it for your own
recording. Ctrl+C passes cancellation to asynchronous analysis.

Short clean solo melodies are the intended input. Analysis is limited to 120
seconds. Transcription is approximate; this synthetic fixture does not prove
accuracy on real recordings. See the
[tutorial](../../docs/tutorials/media-round-trip.md) and
[end-to-end guide](../../docs/nuget-end-to-end.md).
