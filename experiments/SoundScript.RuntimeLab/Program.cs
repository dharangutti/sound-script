using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SoundScript.Media;
using SoundScript.RuntimeLab;
using SoundScript.Wave.Io;

var output = Path.GetFullPath(args.Length == 0 ? "artifacts/runtime-lab" : args[0]);
Directory.CreateDirectory(output);
var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", "monitor.ss"));
var timer = Stopwatch.StartNew();
var runtime = RuntimeProgram.Compile(source);
timer.Stop();
var coldCompileMs = timer.Elapsed.TotalMilliseconds;
var states = new[] { (Name: "normal", Intensity: .25m, X: 200m), (Name: "warning", Intensity: .55m, X: 550m), (Name: "critical", Intensity: .90m, X: 900m) };
var evidence = new List<object>();
foreach (var state in states)
{
    runtime.Set("intensity", state.Intensity);
    runtime.Set("xpos", state.X);
    var frame = runtime.Bind();
    var audio = frame.RenderAudio();
    var scene = frame.SceneAt(TimeSpan.FromSeconds(2));
    var json = TemporalVisualJson.Serialize(scene);
    var svg = TemporalSvgRenderer.Render(scene);
    if (!audio.SequenceEqual(frame.RenderAudio()) || json != TemporalVisualJson.Serialize(frame.SceneAt(TimeSpan.FromSeconds(2))))
        throw new InvalidOperationException("Repeat determinism failed.");
    File.WriteAllBytes(Path.Combine(output, state.Name + ".wav"), audio);
    File.WriteAllText(Path.Combine(output, state.Name + ".json"), json);
    File.WriteAllText(Path.Combine(output, state.Name + ".svg"), svg);
    using var stream = new MemoryStream(audio);
    var samples = WavReader.ReadMono(stream);
    evidence.Add(new { state.Name, state.Intensity, state.X, SceneCenterX = scene.Primitives[0].Left + scene.Primitives[0].Width / 2,
        scene.Primitives[0].Opacity, Rms = Math.Sqrt(samples.Average(v => (double)v * v)),
        WavSha256 = Hash(audio), JsonSha256 = Hash(Encoding.UTF8.GetBytes(json)), SvgSha256 = Hash(Encoding.UTF8.GetBytes(svg)) });
}

// Warm all code paths before measuring; report distributions of batch averages,
// including allocation costs, without asserting a hardware-specific time budget.
for (var i = 0; i < 100; i++) _ = RuntimeProgram.Compile(source);
for (var i = 0; i < 1000; i++) { runtime.Set("intensity", i % 2 == 0 ? .25m : .90m); _ = runtime.Bind(); }
var compile = Measure(500, i => { _ = RuntimeProgram.Compile(source); });
var set = Measure(20000, i => runtime.Set("intensity", i % 2 == 0 ? .25m : .90m));
var bind = Measure(10000, i => { runtime.Set("intensity", i % 2 == 0 ? .25m : .90m); runtime.Set("xpos", i % 2 == 0 ? 200m : 900m); _ = runtime.Bind(); });
var render = Measure(8, i => { runtime.Set("intensity", i % 2 == 0 ? .25m : .90m); _ = runtime.RenderAudio(); });
var sceneQuery = Measure(2000, _ => { runtime.SceneAt(TimeSpan.FromSeconds(2)); });
var report = new { Environment = new { Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription, Environment.ProcessorCount },
    ColdCompileMilliseconds = coldCompileMs, WarmCompile = compile, Set = set, SetTwoAndBind = bind,
    RenderAudio = render, SceneAt = sceneQuery, CompileToBindRatio = compile.MedianMicroseconds / bind.MedianMicroseconds,
    runtime.Counters, runtime.Revision, runtime.BindCount, States = evidence };
var formatted = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(Path.Combine(output, "evidence.json"), formatted);
File.WriteAllText(Path.Combine(output, "index.html"), """
<!doctype html><html lang="en"><meta charset="utf-8"><title>SoundScript RuntimeLab — adaptive monitoring</title>
<style>body{font:16px system-ui;background:#101827;color:#e2e8f0;margin:40px}main{display:flex;gap:20px}article{flex:1;background:#1e293b;padding:20px;border-radius:12px}img{width:100%;background:#0f172a}audio{width:100%}code{color:#7dd3fc}</style>
<h1>One compiled program. Three monitoring states.</h1><p>The same intensity parameter controls cue gain and indicator opacity; xpos controls position.</p>
<p>Click each audio control to compare. These are complete deterministic renders, with no continuous playback engine.</p><main>
<article><h2>Normal</h2><code>intensity=.25; xpos=200</code><img src="normal.svg" alt="Normal state indicator"><audio controls src="normal.wav"></audio></article>
<article><h2>Warning</h2><code>intensity=.55; xpos=550</code><img src="warning.svg" alt="Warning state indicator"><audio controls src="warning.wav"></audio></article>
<article><h2>Critical</h2><code>intensity=.90; xpos=900</code><img src="critical.svg" alt="Critical state indicator"><audio controls src="critical.wav"></audio></article>
</main><p>See evidence.json for output hashes, compiler counts and timings.</p></html>
""");
Console.WriteLine(formatted);
Console.WriteLine($"Demo: {Path.Combine(output, "index.html")}");

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
static Measurement Measure(int iterations, Action<int> action)
{
    var timings = new List<double>();
    var allocations = new List<double>();
    for (var batch = 0; batch < 7; batch++)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < iterations; i++) action(i);
        timings.Add(Stopwatch.GetElapsedTime(start).TotalMicroseconds / iterations);
        allocations.Add((GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)iterations);
    }
    timings.Sort(); allocations.Sort();
    return new(iterations, 7, timings[3], timings[0], timings[^1], allocations[3]);
}
internal sealed record Measurement(int IterationsPerBatch, int Batches, double MedianMicroseconds,
    double MinMicroseconds, double MaxMicroseconds, double MedianAllocatedBytes);
