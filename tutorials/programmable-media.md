# Tutorial: one program, sound and motion

Build a moving status indicator in the Playground, then use the same source in a .NET application.
The Playground uses the existing engine; the V14 package exposes that engine through a small public API.

## 1. Try the source

Open the Playground's **Audio/Visual** workspace. Replace its source with this program, then compile it:

```soundscript
tempo 120
track cue { C4 q E4 q G4 h }
sync audio
visual "status" for 4s {
    shape circle
    fill "#16a34a"
    set width 120
    set height 120
    set y 360
    animate x 200 -> 1080 over 4s
}
visual "caption" for 4s at 0s {
    shape text
    text "SYSTEM HEALTHY"
    fill "#16a34a"
    set x 640
    set y 120
}
```

Play the preview. Move the timeline control to **2 seconds**: the circle's center is at logical x=640.
At **4 seconds**, both visuals are absent. The intervals include their start and exclude their end.
Repeat the same query and it produces the same scene, independent of which time you visited before it.

## 2. Change application data

Change tempo to 160, notes to G5, fill to `#dc2626`, width/height to 220 and caption to `SYSTEM CRITICAL`.
Compile again. These are meaningful audio and visual changes to the same program. In an application,
validate typed data and let your own source builder choose these values; do not paste unchecked user
strings into source. The samples implement Healthy, Warning and Critical with a typed enum.

## 3. Use the package

Install the locally packed V14 candidate following the [NuGet guide](../nuget.md). In a .NET 10 console app,
save the source above as `status.ssv` and write:

```csharp
using SoundScript;
using SoundScript.Media;

var media = SoundScriptEngine.CompileFile("status.ssv").CompileMedia();
File.WriteAllBytes("status.wav", media.RenderAudio());
var scene = media.SceneAt(TimeSpan.FromSeconds(2));
File.WriteAllText("status.json", TemporalVisualJson.Serialize(scene));
File.WriteAllText("status.svg", TemporalSvgRenderer.Render(scene));
Console.WriteLine(media.Duration);
```

Open the SVG in a browser. No FFmpeg, CLI process or browser framework was involved in producing it.
The [console sample](../../samples/ProgrammableMedia/README.md) additionally writes MIDI, stereo WAV,
scene snapshots and SHA-256 hashes for all three scenarios.

## 4. Synchronize a host

Use the actual audio player position as the query time. The [plain HTML sample](../../samples/ProgrammableMediaWeb/README.md)
uses `audio.currentTime` and supports Play, Pause, Resume, Restart and Seek. A desktop app supplies its
own player's `Position`. Repainting can run on a timer, but that timer must not advance a separate media clock.

The typed scene can be mapped to SVG, Canvas, HTML/CSS or desktop controls. The provided SVG adapter
paints canonical geometry; it does not reproduce every legacy Playground decoration. Text shapes keep
the engine's existing Latin bitmap glyph limits. Unicode legacy labels depend on host fonts.

Continue with the [runtime API guide](../programmatic-media-runtime.md) for precise timing, schema,
security, sandbox and memory contracts, or read the [V14 article](../articles/programmable-media-runtime-dotnet.md).
