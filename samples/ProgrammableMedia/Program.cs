using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ProgrammableMedia;
using SoundScript;
using SoundScript.Media;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/programmable-media");
Directory.CreateDirectory(output);
var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
void Save(string name, byte[] bytes)
{
    File.WriteAllBytes(Path.Combine(output, name), bytes);
    hashes[name] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
void Text(string name, string text) => Save(name, Encoding.UTF8.GetBytes(text));
foreach (var status in Enum.GetValues<EquipmentStatus>())
{
    var name = status.ToString().ToLowerInvariant();
    var source = new MonitoringScenario(status).BuildSource();
    var compilation = SoundScriptEngine.Compile(source);
    var media = compilation.CompileMedia();
    var scene = media.SceneAt(TimeSpan.FromSeconds(2));
    var json = TemporalVisualJson.Serialize(scene);
    var svg = TemporalSvgRenderer.Render(scene);
    using var parsed = JsonDocument.Parse(json);
    _ = XDocument.Parse(svg);
    Text(name + ".ssv", source);
    Save(name + ".wav", media.RenderAudio());
    Save(name + ".mid", compilation.RenderMidi());
    Save(name + "-stereo.wav", compilation.RenderStereoWave());
    Text(name + ".json", json);
    Text(name + ".svg", svg);
    Text(name + "-snapshots.json", JsonSerializer.Serialize(new[] { 0d, 1, 2, 3.999, 4, 5 }
        .Select(t => media.SceneAt(TimeSpan.FromSeconds(t)))));
    Console.WriteLine($"{status}: {media.Duration.TotalSeconds:F3}s; {scene.Primitives.Count} primitives");
}
Text("security.svg", TemporalSvgRenderer.Render(new(0, new[] {
    new TemporalVisualPrimitive("\" onload=\"alert(1)", "generic", "<script>alert(1)</script> <img src=x onerror=alert(1)> < > & \" ' 日本語", 0, 0, 1280, 100, 1, 0)
})));
File.WriteAllText(Path.Combine(output, "hashes.json"), JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Artifacts and SHA-256 inventory: " + output);
