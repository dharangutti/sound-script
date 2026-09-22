# Industrial monitoring media

A .NET 10 application consuming published `SoundScript` **13.0.2** with a
`PackageReference`. One typed scenario builder generates `.ss`, `.ssw` and
`.ssv` programs from temperature, vibration, load and status. This demonstrates
monitoring sonification, not a certified alarm or safety controller.

From the repository root:

```sh
dotnet build samples/IndustrialMonitoring -c Release
dotnet run --project samples/IndustrialMonitoring -c Release -- all
dotnet run --project samples/IndustrialMonitoring -c Release -- samples/IndustrialMonitoring/Scenarios/healthy.json artifacts/my-monitoring
```

Arguments are `[all|scenario.json] [output-directory] [ffmpeg-executable]`.
Default output: `artifacts/samples/industrial-monitoring`. FFmpeg can also be
selected with `SOUNDSCRIPT_FFMPEG`; otherwise it is found on PATH. Ctrl+C
cancels between scenarios and during frame generation/encoding. Core Wave
synthesis and preflight are synchronous.

Each `healthy`, `warning`, or `critical` directory contains:

- `<status>.ss`, `.ssw`, `.ssv`: generated, editable source.
- `<status>.mid`, `-music.wav`, `-stereo.wav`, `-alert.wav`.
- `<status>-synchronized.wav`: audio fitted to the four-second timeline.
- `timeline.json`, `scenes.json`: observations at 0, 2 and 3.99 seconds.
- `scenario.json`: typed inputs, tempo/note count and WebM completion status.
- `<status>.webm`: 640×360, 24 FPS VP9/Opus, when FFmpeg supports the codecs.

A missing FFmpeg dependency skips only WebM and removes any previous video
for that scenario. Other failures return a nonzero exit code. Outputs of the
same status replace earlier outputs; use separate output directories to compare
equipment or edits. Generated files are not repository assets.

Edit the four telemetry/status fields and rerun. See the
[step-by-step tutorial](../../docs/tutorials/industrial-monitoring.md),
[mapping and parity guide](../../docs/nuget-end-to-end.md), and
[validation script](../../tools/Validate-NuGetExamples.ps1).
