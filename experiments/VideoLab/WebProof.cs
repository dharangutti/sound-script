using System.Text.Json;

namespace VideoLab;

// Browser data always comes from the real engine, never a second JavaScript model.
internal static class WebProof
{
    public static async Task Publish()
    {
        var output = Path.GetFullPath(Path.Combine("artifacts", "web-build-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var demos = new List<object>();
        int repeatChecks = 0;
        var cases = new[]
        {
            (Id: "demo", Title: "Composition MVP", Description: "Two clips, a crossfade, animated overlay and mixed audio. Compare quieter/left and louder/right bindings.", Frame: 98, Parameter: "accentX", Value: 180m),
            (Id: "transforms", Title: "Transforms, easing and gain", Description: "Position, size, scale, rotation, opacity, crop and pivot with eased keyframes. Audio gain steps at frame 15; compare startX bindings.", Frame: 15, Parameter: "startX", Value: 60m),
            (Id: "expressions", Title: "Expressions and parameter variants", Description: "Frame and progress drive motion, rotation and opacity. Two frozen offset bindings demonstrate the shared-composition pattern used by batch exports.", Frame: 15, Parameter: "offset", Value: 20m),
            (Id: "effects", Title: "Reusable effects", Description: "Red zoom, green fade, yellow slide and blue pulse use reusable effect definitions with independent arguments.", Frame: 15, Parameter: "", Value: 0m),
            (Id: "conditional", Title: "Conditional layers", Description: "A green layer requires score > 70. A yellow layer appears after progress > 0.5. Excluded layers stay visible in the inspector with Included=false.", Frame: 16, Parameter: "score", Value: 10m),
            (Id: "data-sequence", Title: "Data-driven sequences", Description: "Two data records generate trimmed clips, a six-frame crossfade and a shrinking transform. Inspect the expanded engine state alongside the original data declaration.", Frame: 15, Parameter: "", Value: 0m)
        };
        foreach (var item in cases)
        {
            var source = File.ReadAllText($"examples/{item.Id}.json");
            var composition = Composition.Compile(source, Path.GetFullPath("examples"));
            var runtime = composition.CreateRuntime();
            var snapshots = new List<object>();
            foreach (var name in item.Parameter == "" ? new[] { "A" } : new[] { "A", "B" })
            {
                if (name == "B")
                {
                    var changes = new Dictionary<string, decimal> { [item.Parameter] = item.Value };
                    if (item.Id == "demo") changes["musicGain"] = 0.6m;
                    runtime.SetMany(changes);
                }
                var snapshot = runtime.Bind();
                var stem = item.Id == "demo" ? name : $"{item.Id}-{name}";
                snapshots.Add(new { name, stem, parameters = snapshot.Values,
                    scenes = Enumerable.Range(0, composition.Script.Frames).Select(snapshot.SceneAt).ToArray() });
                foreach (var extension in new[] { "mp4", "webm" })
                {
                    var rendered = $"artifacts/{stem}.{extension}";
                    if (item.Id != "demo")
                    {
                        Console.WriteLine($"Gallery: rendering {stem}.{extension} twice...");
                        var repeat = $"artifacts/{stem}-repeat.{extension}";
                        await Ffmpeg.Render(snapshot, rendered); await Ffmpeg.Render(snapshot, repeat);
                        Composition.Require(Ffmpeg.Hash(rendered) == Ffmpeg.Hash(repeat), $"Gallery repeat mismatch: {stem}.{extension}");
                        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-xerror", "-i", rendered, "-f", "null", "-"]);
                        repeatChecks++;
                    }
                    File.Copy(rendered, Path.Combine(output, $"{stem}.{extension}"), true);
                }
            }
            demos.Add(new { id = item.Id, title = item.Title, description = item.Description, inspectFrame = item.Frame,
                fps = composition.Script.Fps, frames = composition.Script.Frames,
                width = composition.Script.Width, height = composition.Script.Height,
                script = JsonSerializer.Deserialize<JsonElement>(source), snapshots });
        }
        // Sort all JSON object keys so immutable dictionary enumeration order cannot
        // make the packaged timeline depend on a process's randomized string hashes.
        WriteJson(Path.Combine(output, "proof.json"), new { schemaVersion = 2, demos });
        foreach (var file in Directory.GetFiles("web", "*", SearchOption.TopDirectoryOnly)
            .Where(p => new[] { ".html", ".css", ".js" }.Contains(Path.GetExtension(p))))
            File.WriteAllText(Path.Combine(output, Path.GetFileName(file)), File.ReadAllText(file).Replace("\r\n", "\n"));
        var hashes = Directory.GetFiles(output).Where(p => Path.GetFileName(p) != "checksums.json")
            .ToDictionary(p => Path.GetFileName(p)!, Ffmpeg.Hash);
        WriteJson(Path.Combine(output, "checksums.json"), hashes);
        var destination = Path.GetFullPath("web/site");
        var previous = Path.GetFullPath(Path.Combine("artifacts", "web-previous-" + Guid.NewGuid().ToString("N")));
        if (Directory.Exists(destination)) Directory.Move(destination, previous);
        try { Directory.Move(output, destination); }
        catch { if (Directory.Exists(previous)) Directory.Move(previous, destination); throw; }
        Console.WriteLine($"PASS: {repeatChecks} gallery format/binding pairs repeated byte-identically and fully decoded. Published {cases.Length} demos.");
    }

    private static void WriteJson(string path, object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            void Write(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    { writer.WritePropertyName(property.Name); Write(property.Value); }
                    writer.WriteEndObject();
                }
                else if (element.ValueKind == JsonValueKind.Array)
                { writer.WriteStartArray(); foreach (var child in element.EnumerateArray()) Write(child); writer.WriteEndArray(); }
                else element.WriteTo(writer);
            }
            Write(JsonSerializer.SerializeToElement(value));
        }
        File.WriteAllBytes(path, stream.ToArray());
    }
}
