using System.Diagnostics;
using System.Text.Json;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;

// Run from the repository root. This calls the exact scene and raster paths used
// by Playground and CLI, without changing either command model or media clock.
var directory = Path.GetFullPath(args.ElementAtOrDefault(0) ?? "obj/visual-parity");
var label = args.ElementAtOrDefault(1) ?? "after";
Directory.CreateDirectory(directory);
var cases = new (string Name, string Source, double Time, int Width, int Height)[] {
    ("legacy-cards", "tools/VisualParity/legacy.ssv", 1, 1280, 720),
    ("legacy-cards-small", "tools/VisualParity/legacy.ssv", 1, 640, 360),
    ("primitives-0", "tools/VisualParity/primitives.ssv", 0, 1280, 720),
    ("primitives-1", "tools/VisualParity/primitives.ssv", 1, 1280, 720),
    ("primitives-2", "tools/VisualParity/primitives.ssv", 2, 1280, 720),
    ("primitives-small", "tools/VisualParity/primitives.ssv", 1, 640, 360),
    ("org-chart", "examples/visual-org-chart.ssv", 4.5, 1280, 720),
    ("information-cards", "examples/visual-information-cards.ssv", 4, 1280, 720),
    ("legacy-0", "examples/visual-temporal.ssv", 0, 1280, 720),
    ("legacy-3", "examples/visual-temporal.ssv", 3, 1280, 720),
    ("legacy-8", "examples/visual-temporal.ssv", 8, 1280, 720),
    ("legacy-small", "examples/visual-temporal.ssv", 8, 640, 360),
};
var results = new List<object>();
foreach (var item in cases)
{
    var timeline = VisualInterpreter.Interpret(ProgramLoader.Load(item.Source).Program);
    var scene = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(item.Time)));
    File.WriteAllText(Path.Combine(directory, item.Name + ".scene.json"), JsonSerializer.Serialize(scene,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    // Warm up before timing; assert repeatability independently of wall-clock speed.
    var pixels = TemporalVideoFrameRenderer.RenderPpm(scene, item.Width, item.Height);
    var timer = Stopwatch.StartNew();
    var again = TemporalVideoFrameRenderer.RenderPpm(scene, item.Width, item.Height);
    timer.Stop();
    if (!pixels.SequenceEqual(again)) throw new Exception("Non-deterministic frame: " + item.Name);
    File.WriteAllBytes(Path.Combine(directory, item.Name + "." + label + ".ppm"), pixels);
    results.Add(new { item.Name, item.Source, item.Time, item.Width, item.Height, Milliseconds = timer.Elapsed.TotalMilliseconds });
    Console.WriteLine($"{item.Name}: {timer.Elapsed.TotalMilliseconds:0.##} ms");
}
File.WriteAllText(Path.Combine(directory, label + ".timings.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
