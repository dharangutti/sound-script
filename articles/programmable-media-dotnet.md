# SoundScript NuGet End to End: From Application Data to Audio/Visual Media — and Back Again

An application often knows why a cue should change before a designer can
produce another asset. A pump's load increases, vibration becomes noticeable,
and its status moves from healthy to critical. Instead of selecting an unrelated
recording, a .NET application can map those values to a small, editable program
and regenerate the media.

SoundScript's NuGet package makes that workflow available inside C#. It can
compile musical source into MIDI and WAV, evaluate temporal visuals, export
synchronized audio/video through FFmpeg, and transcribe suitable audio into an
editable score. The two working applications discussed here consume published
version 13.0.2 directly. This article is an unpublished draft; its examples link
to the source that builds and runs.

## Start with the package and application data

Use a .NET 10 project:

```sh
dotnet add package SoundScript --version 13.0.2
```

The [IndustrialMonitoring sample](../../samples/IndustrialMonitoring) starts
with this model, excerpted from its scenario file:

```csharp
public enum EquipmentStatus { Healthy, Warning, Critical }

public sealed record MonitoringScenario(
    string EquipmentId, double Temperature, double Vibration, double Load, EquipmentStatus Status)
// Validation body omitted; see the sample for the complete record.
```

The full record also validates the identifier, finite numerical ranges and
status. The application reads JSON into that model, then calls one shared
builder. It does not build command-line strings or launch SoundScript as a
child process.

## Change four values, regenerate the media

The healthy fixture describes PUMP-07 at temperature 68, vibration 0.14 and load
52. Its generated score plays two staccato C5 notes at 90 BPM with a quiet flute
timbre. The Wave variant adds a small delay. The status card displays the
equipment and telemetry beside a green circle that fades in over three seconds.

Change temperature to 96, vibration to 0.82, load to 94 and status to Critical.
The builder now generates eight C6 notes at 139 BPM with ff violin dynamics.
Delay mix increases, the circle becomes larger and red, and its fade reaches
full opacity in one second. There are no manually redesigned audio or video
assets. Temperature chooses a pitch band, load and status set tempo, vibration
sets delay, and status controls the remaining cue characteristics.

The intermediate Warning fixture sits between them: 116 BPM, four G5 notes,
mf piano, and an amber circle. These mappings demonstrate sonification and
media generation; they are not calibrated alarm thresholds or a replacement
for industrial protection systems.

Run all three from the repository root:

```sh
dotnet run --project samples/IndustrialMonitoring -c Release
```

The [tutorial](../tutorials/industrial-monitoring.md) walks through editing just
those four values and comparing the resulting source and artifacts.

## Three extensions, one program model

The application saves `.ss` for conventional music, `.ssw` for the Wave-oriented
variant and `.ssv` for the combined visual/audio program. These are conventions
over a shared grammar, not independent language implementations.

For music, the package facade compiles source and returns MIDI or mono/stereo
WAV bytes. Those bytes can be saved, uploaded to object storage or returned by
an HTTP endpoint. The sample writes them to make comparison straightforward.

The advanced visual APIs expose a different useful result: a function of time.
`VisualTimeline.StateAt(t)` evaluates the active elements and automation at any
instant, without first generating video frames. `TemporalVisualSceneBuilder`
projects that state into a presentation scene. The sample records states at
zero, two and 3.99 seconds; the visual interval ends at four seconds.

Only export introduces frame rate. The program samples 96 scenes at 24 FPS,
renders the Wave audio rail to the same four-second duration and passes frames
and audio to `FfmpegWebmExporter.EncodeAndVerify`. That packaged API verifies
both streams by decoding the encoded file. FFmpeg is the external codec
dependency; a missing installation leaves the MIDI, WAV and temporal data
usable and produces a clear video skip message.

## The opposite direction: recording to code

The [MediaRoundTrip application](../../samples/MediaRoundTrip) starts with an
original four-note sine-wave fixture. It decodes native PCM WAV and transcribes
it in monophonic mode, retaining `TranscriptionResult` rather than immediately
flattening the result to text. A report preserves observations, confidence,
diagnostics and suitability. Generation proceeds only when the suitability
gate permits it.

The score writer produces `transcribed.ss`. A small typed edit changes the
instrument from flute to piano, after which the same writer produces
`modified.ss`. Compilation renders the modified source into MIDI and WAV.
Both source versions enable `perform expressive`, so Wave applies the selected
instrument timbre. A MIDI instrument change alone in the default Wave mode
would not produce this audible difference.
Here is the actual edit, excerpted from the sample:

```csharp
var modified = score with { Tracks = score.Tracks.Select(track => track with { Instrument = 0 }).ToArray() };
string source = "perform expressive\n" + writer.Source(modified);
```

Run the complete flow:

```sh
dotnet run --project samples/MediaRoundTrip -c Release
```

The result demonstrates recording → score → edit → reproducible playback.
It does not establish universal transcription accuracy. The fixture is clean
and synthetic, the analysis is bounded to 120 seconds, and real recordings
need suitability review. Experimental mixed-role results are symbolic estimates,
not separated stems. Reconstruction consistency is not source ground truth.

## How much is available without the CLI?

The [package audit](../nuget-end-to-end.md#verified-cli--nuget-parity) answers
that question per workflow. The small facade handles compilation and MIDI/WAV
rendering. Public bundled components cover composition, prosody, source printing,
SoundCSS, temporal visuals, WebM, transcription, vocals and wordbank operations.
SoundCSS can render MIDI to WAV or OGG without FFmpeg. Built-in prosody vocals
need no external executable; other engines and corpus workflows have their
own requirements.

The CLI still provides conveniences that are not a packaged API: consolidated
validation/inspection metadata, some diagnostic policy, transcription completion
manifests and full video-preflight reports. It would be misleading to call the
entire CLI identical to the facade. It would also be misleading to describe
WebM as CLI-only: the reusable export implementation is public and packaged.

No new library facade was necessary for these examples. Existing typed options,
scores, timelines and byte outputs work well. A/V orchestration requires the
most application code because frame generation, temporary files, cancellation
and codecs remain explicit. That is a documented integration cost, not a reason
to wrap command-line handlers in a library method.

## Make reproducibility testable

The validation script builds consumers outside the repository with a clean
NuGet cache and runs each flow twice. It compares source, MIDI, PCM and JSON
hashes, inspects musical notes and instruments, checks time-dependent scenes,
and exercises the missing-FFmpeg path. WebM is decode-verified rather than
promised byte-identical across encoder versions. Determinism depends on the
same inputs, assets, options, engine version and execution environment.

This approach makes media generation ordinary application logic that can be
reviewed and tested. Start with the [end-to-end guide](../nuget-end-to-end.md),
change the monitoring values, and inspect the source alongside the resulting
audio and status cards.
