# From WAV to editable SoundScript and back

This tutorial uses .NET 10 and published SoundScript 13.0.2. Run from the
repository root:

```sh
dotnet run --project samples/MediaRoundTrip -c Release
```

The sample generates `artifacts/samples/media-round-trip/Input/melody.wav`:
four original sine notes, C4, E4, G4 and C5, separated by silence. The fixture
is synthesized directly into PCM, so no recording license or external tool is
needed, and input creation does not depend on SoundScript's Wave synthesizer.

## Follow the structured result

`PcmWaveInput.Decode` converts native WAV to analysis audio.
`TranscriptionEngine.TranscribeAsync` returns the score and observations.
Open `Output/report.json`: it preserves pitch observations, diagnostics,
confidence and suitability. The sample rejects unsuitable input before writing
a reconstruction, keeping the report to explain why.

`TranscriptionSuitability.ScoreForGeneration` selects the supported score;
`SoundScriptOutput.Source` produces `transcribed.ss`. Open it to inspect the
four pitches, rests and flute instrument (73).

## Make a meaningful edit

The sample already changes the typed score from flute to piano (0), using a
record `with` expression. The observation timings remain available in the
report. The exact excerpt in [Program.cs](../../samples/MediaRoundTrip/Program.cs):

```csharp
var modified = score with { Tracks = score.Tracks.Select(track => track with { Instrument = 0 }).ToArray() };
string source = "perform expressive\n" + writer.Source(modified);
```

`modified.ss` is compiled to `reconstructed.mid` and `reconstructed.wav`.
The MIDI has a different instrument program and the WAV has a different timbre.
Both source files start with `perform expressive`, which enables instrument
timbres in Wave; the default Wave mode would not demonstrate this instrument edit.
To try another instrument, change `Instrument = 0` to a supported MIDI program
such as 40 (violin), then run again. The validated default is piano; keep it
when running the repository verification script.

## Use your own input

```sh
dotnet run --project samples/MediaRoundTrip -c Release -- artifacts/own-roundtrip path/to/solo.wav
```

Use a short clean solo PCM WAV and review the report before trusting the output.
The sample fixes tempo at 120 BPM for its known fixture; adjust the typed
`TranscriptionOptions` for other material. Native decoding accepts PCM
8/16/24/32-bit and IEEE float32 WAV. The analysis contract is mono 16 kHz with
a 120-second limit. Other desktop formats use `DesktopMediaInput` and FFmpeg;
that advanced path is available in the package but is outside this example.

This is not universal or perfect transcription. Noise, overlapping instruments,
rubato and effects can reduce usefulness. Reconstructing and reanalyzing the
result measures consistency, not the ground truth of the original recording.
Ctrl+C cancels supported analysis stages. A rejected input returns a nonzero
exit code and leaves no old successful reconstruction disguised as a new one.

See the [central guide](../nuget-end-to-end.md) for package parity and the
[validation report](../nuget-end-to-end-validation.md) for tested behavior.
