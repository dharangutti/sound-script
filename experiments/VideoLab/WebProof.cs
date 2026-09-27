using System.Text.Json;
using System.Text.Json.Nodes;

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
            (Id: "showcase", Title: "Video + audio showcase", Description: "Real coast and companion footage, two trimmed clips, a crossfade, a moving graphic and mixed audio. Switch the sample video or audio below.", Frame: 98, Parameter: "accentX", Value: 180m),
            (Id: "audience", Title: "One composition, three audiences", Description: "The same synthetic footage becomes shop-floor instructions, QA inspection or engineering review. Audience groups change titles, callouts and markers; the source composition stays identical.", Frame: 30, Parameter: "", Value: 0m),
            (Id: "editing", Title: "Titles, callouts and familiar edits", Description: "Two trimmed clips, crossfade, title, moving callout, shape and audio. Explore position, scale, rotation, opacity, crop and gain in local mode; source defines timing.", Frame: 30, Parameter: "position", Value: 30m),
            (Id: "demo", Title: "Composition MVP", Description: "Two clips, a crossfade, animated overlay and mixed audio. Compare quieter/left and louder/right bindings.", Frame: 98, Parameter: "accentX", Value: 180m),
            (Id: "transforms", Title: "Transforms, easing and gain", Description: "Position, size, scale, rotation, opacity, crop and pivot with eased keyframes. Audio gain steps at frame 15; compare startX bindings.", Frame: 15, Parameter: "startX", Value: 60m),
            (Id: "expressions", Title: "Expressions and parameter variants", Description: "Frame and progress drive motion, rotation and opacity. Two frozen offset bindings demonstrate the shared-composition pattern used by batch exports.", Frame: 15, Parameter: "offset", Value: 20m),
            (Id: "effects", Title: "Reusable effects", Description: "Red zoom, green fade, yellow slide and blue pulse use reusable effect definitions with independent arguments.", Frame: 15, Parameter: "", Value: 0m),
            (Id: "conditional", Title: "Conditional layers", Description: "A green layer requires score > 70. A yellow layer appears after progress > 0.5. Excluded layers stay visible in the inspector with Included=false.", Frame: 16, Parameter: "score", Value: 10m),
            (Id: "data-sequence", Title: "Data-driven sequences", Description: "Two data records generate trimmed clips, a six-frame crossfade and a shrinking transform. Inspect the expanded engine state alongside the original data declaration.", Frame: 15, Parameter: "", Value: 0m)
        };
        foreach (var item in cases)
        {
            var original = File.ReadAllText($"examples/{item.Id}.json");
            var originalComposition = Composition.Compile(original, Path.GetFullPath("examples"));
            var snapshots = new List<object>(); string? firstStem = null;
            var variants = item.Id == "showcase"
                ? new[] { (Video: "montage", Audio: "calm"), (Video: "pattern", Audio: "calm"), (Video: "montage", Audio: "pulse"), (Video: "pattern", Audio: "pulse") }
                : new[] { (Video: "original", Audio: "original") };
            foreach (var variant in variants)
            {
                var node = JsonNode.Parse(original)!;
                if (item.Id == "showcase")
                {
                    foreach (var clip in node["videos"]!.AsArray()) clip!["asset"] = $"../web/samples/{variant.Video}.mp4";
                    node["audio"]![0]!["asset"] = $"../web/samples/{variant.Audio}.wav";
                }
                var source = node.ToJsonString();
                var composition = Composition.Compile(source, Path.GetFullPath("examples"));
                var runtime = composition.CreateRuntime();
                foreach (var name in item.Id == "audience" ? new[] { "shopfloor", "qa", "engineering" } : item.Parameter == "" ? new[] { "A" } : new[] { "A", "B" })
                {
                    if (name == "B")
                    {
                        var changes = new Dictionary<string, decimal> { [item.Parameter] = item.Value };
                        if (item.Id is "demo" or "showcase") changes["musicGain"] = 0.6m;
                        if (item.Id == "editing") { changes["rotation"] = 5; changes["scale"] = 0.9m; changes["opacity"] = 0.8m; changes["crop"] = 0.1m; changes["showCallout"] = 0; }
                        runtime.SetMany(changes);
                    }
                    if (item.Id == "audience") runtime.SetBindings(new Dictionary<string, JsonElement> { ["audience"] = JsonSerializer.SerializeToElement(name) });
                    var snapshot = runtime.Bind();
                    var stem = item.Id == "demo" ? name : item.Id == "showcase" ? $"showcase-{variant.Video}-{variant.Audio}-{name}" : $"{item.Id}-{name}";
                    firstStem ??= stem;
                    snapshots.Add(new { name, stem, video = variant.Video, audio = variant.Audio, parameters = snapshot.Bindings,
                        script = JsonSerializer.Deserialize<JsonElement>(source), tracks = Tracks(composition),
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
            }
            var thumb = item.Id + ".jpg";
            await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", $"artifacts/{firstStem}.mp4", "-ss", "0.5", "-frames:v", "1", "-vf", "scale=480:-2", Path.Combine(output, thumb)]);
            demos.Add(new { id = item.Id, title = item.Title, description = item.Description, inspectFrame = item.Frame, thumbnail = thumb,
                fps = originalComposition.Script.Fps, frames = originalComposition.Script.Frames,
                width = originalComposition.Script.Width, height = originalComposition.Script.Height,
                script = JsonSerializer.Deserialize<JsonElement>(original), snapshots });
        }
        Directory.CreateDirectory(Path.Combine(output, "samples"));
        foreach (var file in Directory.GetFiles("web/samples").Where(p => Path.GetExtension(p) is ".mp4" or ".wav"))
            File.Copy(file, Path.Combine(output, "samples", Path.GetFileName(file)));
        // Sort all JSON object keys so immutable dictionary enumeration order cannot
        // make the packaged timeline depend on a process's randomized string hashes.
        WriteJson(Path.Combine(output, "proof.json"), new { schemaVersion = 2, demos });
        foreach (var file in Directory.GetFiles("web", "*", SearchOption.TopDirectoryOnly)
            .Where(p => new[] { ".html", ".css", ".js" }.Contains(Path.GetExtension(p))))
            File.WriteAllText(Path.Combine(output, Path.GetFileName(file)), File.ReadAllText(file).Replace("\r\n", "\n"));
        var hashes = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Where(p => Path.GetFileName(p) != "checksums.json")
            .ToDictionary(p => Path.GetRelativePath(output, p).Replace('\\', '/'), Ffmpeg.Hash);
        WriteJson(Path.Combine(output, "checksums.json"), hashes);
        var destination = Path.GetFullPath("web/site");
        var previous = Path.GetFullPath(Path.Combine("artifacts", "web-previous-" + Guid.NewGuid().ToString("N")));
        if (Directory.Exists(destination)) Directory.Move(destination, previous);
        try { Directory.Move(output, destination); }
        catch { if (Directory.Exists(previous)) Directory.Move(previous, destination); throw; }
        Console.WriteLine($"PASS: {repeatChecks} gallery format/binding pairs repeated byte-identically and fully decoded. Published {cases.Length} demos.");
    }

    internal static object[] Tracks(Composition c) => c.Clips.Select((v, i) => (object)new { kind = "video", label = $"Clip {i + 1}", at = v.At, frames = v.Frames, trim = v.Trim, fade = v.Fade })
        .Concat(c.Clips.Where(v => v.Fade > 0).Select(v => (object)new { kind = "transition", label = "Crossfade", at = v.At, frames = v.Fade }))
        .Concat(c.Script.Audio.Select((a, i) => (object)new { kind = "audio", label = $"Audio {i + 1}", at = a.At, frames = a.Frames }))
        .Concat(c.Script.Shapes.Select((o, i) => (object)new { kind = "overlay", label = $"Shape {i + 1}", at = o.At, frames = o.Frames }))
        .Concat(c.Script.Texts.Select((o, i) => (object)new { kind = "text", label = $"Title {i + 1}", at = o.At, frames = o.Frames }))
        .Concat(c.Script.Callouts.Select((o, i) => (object)new { kind = "callout", label = $"Callout {i + 1}", at = o.At, frames = o.Frames })).ToArray();

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
