using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using SoundScript;
using SoundScript.Media;

var output = Path.GetFullPath(args.FirstOrDefault(a => a != "--benchmark") ?? "artifacts/runtime-parameters");
Directory.CreateDirectory(output);
var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "monitor.ss"));
var initialCompile = Stopwatch.StartNew();
var runtime = SoundScriptEngine.CompileRuntime(source);
initialCompile.Stop();

var initial = runtime.Bind();
foreach (var (name, intensity, xpos) in new[] { ("normal", .25m, 200m), ("warning", .55m, 550m), ("critical", .9m, 900m) })
{
    // Application state supplies data. The source and compiled topology stay fixed.
    runtime.SetMany(new Dictionary<string, decimal> { ["intensity"] = intensity, ["xpos"] = xpos });
    WriteState(name, runtime.Bind());
}
var critical = runtime.Bind();

Console.WriteLine($"Compilation counts: tokenize={runtime.Statistics.Tokenizations}, parse={runtime.Statistics.Parses}, timeline={runtime.Statistics.TimelineCompilations}");
Console.WriteLine($"Snapshot revisions: {initial.Revision} -> {critical.Revision}");
Console.WriteLine($"Audio changed: {Hash(initial.RenderAudio()) != Hash(critical.RenderAudio())}");
Console.WriteLine($"Artifacts: {output}");
if (args.Contains("--benchmark"))
{
    var measurements = new Dictionary<string, object>
    {
        ["warmCompile"] = Measure(() => _ = SoundScriptEngine.CompileRuntime(source), 200),
        ["set"] = Measure(() => runtime.Set("intensity", runtime.Get("intensity") == .25m ? .9m : .25m), 10000),
        ["setAndBind"] = Measure(() => { runtime.Set("intensity", runtime.Get("intensity") == .25m ? .9m : .25m); _ = runtime.Bind(); }, 10000),
        ["cachedBind"] = Measure(() => _ = runtime.Bind(), 10000),
        ["sceneAt"] = Measure(() => _ = runtime.SceneAt(TimeSpan.FromSeconds(2)), 500),
        ["renderAudio"] = Measure(() => _ = runtime.RenderAudio(), 8)
    };
    File.WriteAllText(Path.Combine(output, "benchmark.json"), JsonSerializer.Serialize(new
    {
        note = "Seven-batch medians; observational timings, not latency guarantees. Set includes Get to alternate state.",
        coldCompileMilliseconds = initialCompile.Elapsed.TotalMilliseconds,
        measurements, runtime.Statistics, runtime.Revision, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
    }, new JsonSerializerOptions { WriteIndented = true }));
}

void WriteState(string name, SoundScriptRuntimeSnapshot snapshot)
{
    File.WriteAllBytes(Path.Combine(output, name + ".wav"), snapshot.RenderAudio());
    File.WriteAllBytes(Path.Combine(output, name + ".mid"), snapshot.RenderMidi());
    var json = TemporalVisualJson.Serialize(snapshot.SceneAt(TimeSpan.FromSeconds(2)));
    File.WriteAllText(Path.Combine(output, name + ".json"), json, Encoding.UTF8);
    File.WriteAllText(Path.Combine(output, name + ".svg"), TemporalSvgRenderer.Render(snapshot.SceneAt(TimeSpan.FromSeconds(2))), Encoding.UTF8);
    File.WriteAllText(Path.Combine(output, name + ".sha256"),
        $"wav {Hash(snapshot.RenderAudio())}{Environment.NewLine}json {Hash(Encoding.UTF8.GetBytes(json))}{Environment.NewLine}", Encoding.UTF8);
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static object Measure(Action action, int iterations)
{
    for (var i = 0; i < 20; i++) action();
    var times = new List<double>(); var allocations = new List<double>();
    for (var batch = 0; batch < 7; batch++)
    {
        var timer = new Stopwatch();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        timer.Start();
        for (var i = 0; i < iterations; i++) action();
        timer.Stop();
        times.Add(timer.Elapsed.TotalMicroseconds / iterations);
        allocations.Add((GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)iterations);
    }
    return new { medianMicroseconds = times.Order().ElementAt(3), minimumMicroseconds = times.Min(), maximumMicroseconds = times.Max(), medianAllocatedBytes = allocations.Order().ElementAt(3), iterationsPerBatch = iterations };
}
