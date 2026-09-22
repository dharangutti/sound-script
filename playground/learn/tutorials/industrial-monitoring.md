# Turn telemetry changes into media

You need the .NET 10 SDK and a checkout of the repository. The sample restores
the published SoundScript 13.0.2 NuGet package. For video, optionally install
FFmpeg with VP9 and Opus support; all other outputs work without it.

## 1. Run the three scenarios

From the repository root:

```sh
dotnet run --project samples/IndustrialMonitoring -c Release
```

Open `artifacts/samples/industrial-monitoring/healthy/healthy-music.wav`, then
compare `warning/warning-music.wav` and `critical/critical-music.wav`.
The notes become higher, denser and faster. The corresponding alert WAVs add
delay controlled by vibration. The MIDI files carry the musical notes,
dynamics and instrument changes.

## 2. Change only four values

Open [healthy.json](../../samples/IndustrialMonitoring/Scenarios/healthy.json).
Keep `EquipmentId` unchanged. Change:

| Field | Before | After |
|---|---|---|
| Temperature | 68 | 96 |
| Vibration | 0.14 | 0.82 |
| Load | 52 | 94 |
| Status | Healthy | Critical |

Run that file into a separate output directory:

```sh
dotnet run --project samples/IndustrialMonitoring -c Release -- samples/IndustrialMonitoring/Scenarios/healthy.json artifacts/monitoring-edited
```

The output subdirectory is `critical`, because the scenario's status determines
its name; the input filename does not. Compare it with the original `healthy`
directory. Restore the JSON before running the validation script, which checks
the checked-in scenarios' exact expected mappings.

## 3. Explain the changes

The same [MonitoringSourceBuilder](../../samples/IndustrialMonitoring/MonitoringScenario.cs)
now emits 139 BPM instead of 90, eight staccato C6 eighth notes instead of two
C5 half notes, ff violin instead of mp flute, and delay mix 0.255 instead of
0.085. All notes still occupy four beats; higher tempo makes the cue shorter.
The supplied numbers are demonstration mappings, not protection thresholds.

`critical.ss` explains the musical changes; `critical.ssw` adds the Wave effect;
`critical.ssv` combines that audio program with equipment text and presentation.
The status circle grows from 100 to 260 logical pixels, becomes red and reaches
full opacity after one second instead of three. Text includes the changed
telemetry. This is one builder parameterized by data, not three unrelated assets.

## 4. Inspect time without generating frames

Read `timeline.json` and `scenes.json`. They contain `StateAt(0)`, `StateAt(2)`
and `StateAt(3.99)` observations and their scene projections. The status cue
starts at opacity 0.3 and reaches 1. The visual interval ends at four seconds;
`StateAt(4)` has no active elements. Timeline semantics contain no frame rate.

`*-synchronized.wav` is exactly four seconds. Audio tails are padded or truncated
by the existing temporal audio renderer. With FFmpeg, the application samples
96 scenes at 24 FPS and writes a decode-verified WebM. `scenario.json` records
whether video was verified or skipped. Play the WebM to compare the status card
and cue together.

## 5. Verify repeatability

After restoring any edited fixture:

```powershell
pwsh -NoProfile -File tools/Validate-NuGetExamples.ps1
```

This compares two independent runs, checks MIDI notes/instruments, non-silent
WAVs, visual state and timing, and exercises missing FFmpeg. Source, PCM,
MIDI and scene data should be byte-identical for the same environment, package,
inputs and options. WebM must decode successfully; its container bytes are not
promised to match across encoder versions. See the
[package/API audit](../nuget-end-to-end.md#verified-cli--nuget-parity).
