using System.Security.Cryptography;
using System.Text;
using SoundScript;
using SoundScript.Media;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/runtime-parameters");
Directory.CreateDirectory(output);
var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "monitor.ss"));
var runtime = SoundScriptEngine.CompileRuntime(source);

var initial = runtime.Bind();
WriteState("normal", initial);

runtime.SetMany(new Dictionary<string, decimal>(StringComparer.Ordinal)
{
    ["intensity"] = .90m,
    ["xpos"] = 900m
});
var warning = runtime.Bind();
WriteState("warning", warning);

Console.WriteLine($"Compilation counts: tokenize={runtime.Statistics.Tokenizations}, parse={runtime.Statistics.Parses}, timeline={runtime.Statistics.TimelineCompilations}");
Console.WriteLine($"Snapshot revisions: {initial.Revision} -> {warning.Revision}");
Console.WriteLine($"Audio changed: {Hash(initial.RenderAudio()) != Hash(warning.RenderAudio())}");
Console.WriteLine($"Artifacts: {output}");

void WriteState(string name, SoundScriptRuntimeSnapshot snapshot)
{
    File.WriteAllBytes(Path.Combine(output, name + ".wav"), snapshot.RenderAudio());
    File.WriteAllBytes(Path.Combine(output, name + ".mid"), snapshot.RenderMidi());
    var json = TemporalVisualJson.Serialize(snapshot.SceneAt(TimeSpan.FromSeconds(2)));
    File.WriteAllText(Path.Combine(output, name + ".json"), json, Encoding.UTF8);
    File.WriteAllText(Path.Combine(output, name + ".sha256"),
        $"wav {Hash(snapshot.RenderAudio())}{Environment.NewLine}json {Hash(Encoding.UTF8.GetBytes(json))}{Environment.NewLine}", Encoding.UTF8);
}

static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
