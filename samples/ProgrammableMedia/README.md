# Programmable media for any .NET host

This console sample uses only the SoundScript NuGet package. The shared application-owned
`MonitoringScenario` source builder has no web dependencies. No browser, FFmpeg, UI framework or CLI is used.

From a [source checkout with submodules](../../README.md#try-it-in-60-seconds), run these commands at the repository root:

```powershell
dotnet pack src/SoundScript -c Release -o artifacts/packages
dotnet restore samples/ProgrammableMedia -p:RestoreAdditionalProjectSources=../../artifacts/packages --packages ./artifacts/sample-cache
dotnet run --project samples/ProgrammableMedia -c Release --no-restore -- artifacts/programmable-media
```

The output contains source, MIDI, synchronized WAV, stereo WAV, versioned JSON, SVG, scene snapshots
and a SHA-256 inventory for Healthy, Warning and Critical scenarios. Repeat into a different directory
and compare `hashes.json` to verify deterministic output across processes.

The essential flow is:

```csharp
var media = SoundScriptEngine.Compile(source).CompileMedia();
byte[] wav = media.RenderAudio();
var scene = media.SceneAt(TimeSpan.FromSeconds(2));
string json = TemporalVisualJson.Serialize(scene);
string svg = TemporalSvgRenderer.Render(scene);
```

See [runtime architecture and host integration](../../docs/programmatic-media-runtime.md).
