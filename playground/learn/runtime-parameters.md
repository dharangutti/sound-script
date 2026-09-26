# Runtime parameters

Runtime parameters let a .NET host compile fixed SoundScript structure once, change approved numeric values, and bind a new render snapshot. This is an opt-in addition to the established static `Compile` and `CompileFile` APIs. It does not add live synthesis, streaming, scheduling, or runtime changes to tracks, notes, imports, tempo, or timing.

Runtime source declares decimal parameters and uses them only in supported direct values:

```soundscript
param intensity = 0.25
param xpos = 200

perform expressive
tempo 120
track cue { gain intensity C4 q E4 q G4 h }
visual "indicator" for 4s {
    shape circle
    set x xpos
    set y 360
    set width 120
    set height 120
    set opacity intensity
}
```

`let` and `marker` remain compile-time declarations. Runtime parameters cannot be used in expressions, note/timing positions, tempo, imports, names, or structural decisions. Direct track gain requires the existing `perform expressive` mode. Visual parameter targets are constant `set` values on a uniquely named visual. Supported properties are `x`, `y`, `opacity`, `rotation`, `width`, and `height`. Runtime coordinates and property limits are host validation policy: gain/opacity 0–1, x −12,800–12,800, y −7,200–7,200, width 8–1,280, height 8–720, and rotation −360–360. A shared parameter's range is the intersection of every bound property's range.

## .NET API

```csharp
using SoundScript;
using SoundScript.Media;

var runtime = SoundScriptEngine.CompileRuntime(source);
foreach (var parameter in runtime.Parameters)
    Console.WriteLine($"{parameter.Name}: {runtime.Get(parameter.Name)} ({parameter.Minimum}..{parameter.Maximum})");

var initial = runtime.Bind();
runtime.SetMany(new Dictionary<string, decimal>
{
    ["intensity"] = 0.90m,
    ["xpos"] = 900m
});
var updated = runtime.Bind();
byte[] wav = updated.RenderAudio();
byte[] midi = updated.RenderMidi();
var scene = updated.SceneAt(TimeSpan.FromSeconds(2));
string json = TemporalVisualJson.Serialize(scene);
```

`CompileRuntime` tokenizes/parses the initial source and builds the temporal timeline. `Set`, `SetMany`, `Reset`, and `Get` operate on typed decimal data; updates never parse source. `SetMany` validates the whole batch before changing state. Unknown names, non-decimal values, and values outside a declared range are rejected. A no-op update does not advance `Revision`; a changed batch advances it once. `Bind` returns a stable snapshot, so later updates do not change an earlier frame. `Statistics` exposes actual initial tokenization, parse, and timeline compilation counts for this runtime instance.

`SoundScriptRuntimeSnapshot.RenderAudio()` renders complete mono WAV padded to the visual timeline while preserving audio tails. `RenderWave()` produces the ordinary WAV rail without visual-duration padding, and `RenderMidi()` uses the existing MIDI interpreter with private notation copies. `SceneAt(time)` queries the existing half-open timeline at exact elapsed time and applies bound visual constants. Every path uses the existing SoundScript renderers; the host owns playback.

For a trusted source file, `CompileRuntimeFile(path, allowedRoot)` retains the file's directory for relative sample assets. Runtime file compilation does not resolve SoundScript imports. Use ordinary `CompileFile` when static imports are required; runtime parameters and imported source graphs are separate workflows. Preserve external sample assets while a runtime or snapshot may still render them.

See the self-contained [C# monitoring host sample](../samples/RuntimeParameters/README.md), the existing [programmable media guide](programmatic-media-runtime.md), and the [CLI reference](cli.md#runtime-parameter-snapshots-candidate-api).

## State ownership and lifetime

`runtime.CreateInstance()` reuses the compiled structure with independent default values and revision zero. It performs no parsing. Use it for separate game entities or application sessions; updates and reset on one instance cannot change another. Instances hold managed memory and need no disposal. Release host references when finished; retained snapshots stay usable. `runtime.ToString()` provides a compact revision/value summary.

Names are case-sensitive ASCII letters/digits beginning with a letter. Defaults are mandatory; there is no unresolved required-value state. Passing strings, integers, doubles (including NaN/infinity) or null to `Set` is rejected; use decimal literals such as `0.8m`. A program without parameters is valid and has an empty schema. `SetMany` validates before committing and preserves the last valid state after a rejected batch. Do not mutate the caller's batch dictionary while passing it to `SetMany`.

State operations are synchronized. Bind once for matching audio and scene output; separate convenience calls may capture different revisions when another thread updates between calls. Independent snapshots can render concurrently. Returned media buffers and scene projections do not expose the compiled AST; keep external sample assets stable while rendering.

## CLI

The CLI accepts a single snapshot from a runtime source file:

```bash
soundscript inspect samples/RuntimeParameters/monitor.ss --runtime --params --param intensity=0.9 --param xpos=900 --at 2 --json
soundscript wave samples/RuntimeParameters/monitor.ss --runtime --param intensity=0.9 --param xpos=900 --out critical.wav
soundscript run samples/RuntimeParameters/monitor.ss --runtime --param intensity=0.9 --out critical.mid
```

`inspect --params` lists each declared parameter's decimal default, bounds, and current value. Repeated `--param name=value` options override defaults using invariant decimal notation. The `run` and `wave` commands render one MIDI or ordinary Wave snapshot, respectively; they do not start an interactive session. See [CLI runtime snapshots](cli.md#runtime-parameter-snapshots-candidate-api).

## Playground

Choose **Try V16 adaptive media** or open the Audio/Visual workspace. The **Adaptive media — runtime parameters** panel starts with a compiled monitoring example. Choose **Normal**, **Warning**, or **Critical** to move and brighten the indicator and change the melody's volume, then press Play to hear the new WAV. The browser audio playback clock drives `SceneAt(t)` on the same stable snapshot that supplies the WAV, using existing temporal rendering. Pause, resume, seek, and replay keep the preview synchronized; runtime parameter semantics are unchanged.

Choose **Explore all visual parameters** for an editable rectangle with gain, x, y, width, height, rotation, and opacity controls. Update the discovered controls and choose **Apply state**; the applied values and snapshot revision identify the rendered output. **Reset values** restores declared defaults. Editing source clears the preview and requires **Compile** again. Both examples use `SoundScriptEngine.CompileRuntime`, `SetMany`, and a bound snapshot without recompiling on state changes. The existing static visual examples and their timeline authoring/playback path remain available alongside it. Playback is an offline complete WAV through browser audio controls, not streaming synthesis.

Pre-publication acceptance and external-package checks are recorded in the [V16 acceptance report](v16-acceptance-report.md) and [developer-experience gate](v16-dx-release-gate.md).
