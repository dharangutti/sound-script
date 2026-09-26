using System.Text.Json;

namespace VideoLab;

// The hosted explorer consumes the real engine's finite snapshots and verified renders.
// It deliberately does not reimplement composition semantics in JavaScript.
internal static class WebProof
{
    public static void Publish()
    {
        var output = Path.GetFullPath("web/site");
        Directory.CreateDirectory(output);
        var source = File.ReadAllText("examples/demo.json");
        var composition = Composition.Compile(source, Path.GetFullPath("examples"));
        var runtime = composition.CreateRuntime();
        var snapshots = new List<object>();
        foreach (var name in new[] { "A", "B" })
        {
            if (name == "B") runtime.SetMany(new Dictionary<string, decimal> { ["musicGain"] = 0.6m, ["accentX"] = 180 });
            var snapshot = runtime.Bind();
            snapshots.Add(new { name, parameters = snapshot.Values,
                scenes = Enumerable.Range(0, composition.Script.Frames).Select(snapshot.SceneAt).ToArray() });
            foreach (var extension in new[] { "mp4", "webm" })
                File.Copy($"artifacts/{name}.{extension}", Path.Combine(output, $"{name}.{extension}"), true);
        }
        File.WriteAllText(Path.Combine(output, "proof.json"), JsonSerializer.Serialize(new {
            schemaVersion = 1, fps = composition.Script.Fps, frames = composition.Script.Frames,
            script = JsonSerializer.Deserialize<JsonElement>(source), snapshots
        }));
        foreach (var file in Directory.GetFiles("web", "*", SearchOption.TopDirectoryOnly)
            .Where(p => new[] { ".html", ".css", ".js" }.Contains(Path.GetExtension(p))))
            File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
        var hashes = Directory.GetFiles(output).Where(p => Path.GetFileName(p) != "checksums.json")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToDictionary(p => Path.GetFileName(p)!, Ffmpeg.Hash);
        File.WriteAllText(Path.Combine(output, "checksums.json"), JsonSerializer.Serialize(hashes));
        Console.WriteLine($"Published verified browser proof to {output}");
    }
}
