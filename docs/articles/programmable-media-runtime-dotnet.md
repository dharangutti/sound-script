# SoundScript 14: A Programmable Audio/Visual Runtime for .NET

*Article draft. Not externally published.*

A monitoring application needs an audible cue and a visual status display. Both should reflect the
same data and playback position. SoundScript 14 lets one program define the sound and queryable visual
state, while the application chooses its player and renderer.

```csharp
using SoundScript;
using SoundScript.Media;

var media = SoundScriptEngine.Compile("""
    tempo 120
    track cue { C4 q E4 q G4 h }
    visual "status" for 4s {
        shape circle
        fill "#16a34a"
        animate x 200 -> 1080 over 4s
    }
    """).CompileMedia();
var wav = media.RenderAudio();
var scene = media.SceneAt(TimeSpan.FromSeconds(2));
var svg = TemporalSvgRenderer.Render(scene);
var json = TemporalVisualJson.Serialize(scene);
```

This API is a thin composition of SoundScript's existing deterministic engine. Audio comes from Wave;
visual state comes from `VisualTimeline.StateAt(t)`. No video encoder or browser is required. The scene
is a typed .NET value before it becomes JSON, SVG, custom UI or frames for an optional WebM exporter.

Time is an input. A browser supplies `audio.currentTime`; a desktop host supplies its player's position.
Seeking means querying another time. Pausing means the host stops moving its playback position. There
is no SoundScript timer racing against the audio device. Visual intervals are `[start,end)`, so exact
boundaries and after-end behavior remain deterministic.

Application data can select tempo, notes, colors and shape sizes. The package-backed samples use a typed
Healthy/Warning/Critical scenario and a small source builder. The generated program is reusable and
testable without inventing a generic template framework.

The browser sample is ordinary HTML and JavaScript hosted by ASP.NET Core. It demonstrates playback,
seeking, scenario changes and responsive SVG. The console sample writes source, MIDI, WAV, JSON, SVG
and hashes without a UI. Desktop applications map the same scene to their own controls; no dedicated
WPF, MAUI or Avalonia adapters are claimed.

SVG consumes canonical paths and safely escapes labels. Its simple legacy card presentation does not
reproduce every Playground shadow or gradient. Canonical text retains the engine's existing bitmap
glyph limits. WAV is rendered in memory on first audio/duration access, so applications with large
streaming workloads should use the existing lower-level stream pipeline.

SoundScript defines deterministic audio, visual state and media time. Applications choose playback and
rendering. HTML, SVG, Canvas and WebM are output strategies, not separate SoundScript timing models.

See the [developer guide](../programmatic-media-runtime.md), [console sample](../../samples/ProgrammableMedia/README.md)
and [HTML sample](../../samples/ProgrammableMediaWeb/README.md) for the complete workflow and its tested limits.
