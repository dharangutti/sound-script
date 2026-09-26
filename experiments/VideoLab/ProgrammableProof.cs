using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoLab;

internal static class ProgrammableProof
{
    public static async Task Run()
    {
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException($"FAIL: {name}"); checks.Add(name); }
        void Reject(Action action, string name)
        {
            try { action(); } catch (ArgumentException) { checks.Add(name); return; }
            throw new InvalidOperationException($"FAIL: accepted {name}");
        }
        var context = new EvaluationContext(ImmutableDictionary<string, decimal>.Empty.Add("p", 4), 5, 11, 30, 160, 96);
        decimal E(string source, EvaluationContext? c = null) => Expressions.Parse(source, ["p"]).Evaluate(c ?? context).Number;
        Check(E("2 + 3 * 4") == 14 && E("(2 + 3) * 4") == 20 && E("10-3-2") == 5, "Expression precedence, parentheses and associativity");
        Check(E("frame + progress * fps + canvasWidth / canvasHeight") == 5 + 15 + 160m / 96, "Local frame built-ins");
        Check(E("clamp(abs(-p) * 2, min(1,2), max(3,6))") == 6, "Closed math functions");
        Check(E("progress", context with { Frames = 1, Frame = 0 }) == 0, "One-frame progress is zero");
        Reject(() => Expressions.Parse("missing + 1", []), "Unknown expression variable");
        Reject(() => E("1 / (p-4)"), "Division by zero is a diagnostic");
        Reject(() => Expressions.Parse("System.IO.File()", []), "Host calls unavailable");
        Reject(() => Expressions.Parse(new string('(', 34) + "1" + new string(')', 34), []), "Expression nesting limit");
        Reject(() => Expressions.Parse(string.Join("+", Enumerable.Repeat("1", 40)), []), "Actual AST depth limit");
        Check(E("frame * 0.25", context with { Frame = 9 }) == 2.25m && E("frame * 0.25", context with { Frame = 1 }) == 0.25m && E("frame * 0.25", context with { Frame = 9 }) == 2.25m, "Random-access expression determinism");
        foreach (var (kind, expected) in new[] { (InterpolationKind.Linear, 2.5m), (InterpolationKind.EaseIn, 0.625m), (InterpolationKind.EaseOut, 4.375m), (InterpolationKind.EaseInOut, 1.25m), (InterpolationKind.Step, 0m) })
        {
            var animation = new Animation([new(0, new Literal(0), kind), new(4, new Literal(10), InterpolationKind.Linear)]);
            Check(animation.Evaluate(context with { Frame = 0 }) == 0 && animation.Evaluate(context with { Frame = 1 }) == expected && animation.Evaluate(context with { Frame = 4 }) == 10, $"{kind}: first, interior and last keyframes");
        }
        var adjacent = new Animation([new(0, new Literal(3), InterpolationKind.Step), new(1, new Literal(7), InterpolationKind.Linear)]);
        Check(adjacent.Evaluate(context with { Frame = 1 }) == 7, "Adjacent keyframes choose exact endpoint");
        Check(new Animation([new(0, new Literal(9), InterpolationKind.Linear)]).Evaluate(context with { Frame = 0 }) == 9, "One-frame animation");
        var source = File.ReadAllText("examples/transforms.json"); var directory = Path.GetFullPath("examples");
        Composition Compile(string text) => Composition.Compile(text, directory);
        string Mutate(Action<JsonNode> action) { var node = JsonNode.Parse(source)!; action(node); return node.ToJsonString(); }
        var c = Compile(source); var runtime = c.CreateRuntime(); var a = runtime.Bind();
        runtime.SetMany(new Dictionary<string, decimal> { ["startX"] = 60 }); var b = runtime.Bind();
        Check(ReferenceEquals(a.Composition, b.Composition) && a.SceneAt(0).Layers[0].Transform.X == 20 && b.SceneAt(0).Layers[0].Transform.X == 60, "Generalized transforms preserve frozen snapshot values");
        var end = a.SceneAt(29).Layers[0].Transform;
        Check(end.X == 120 && end.Y == 60 && end.Width == 30 && end.Height == 16 && end.ScaleX == 1.5m && end.Rotation == 90 && end.Opacity == 0.5m, "All generalized animation endpoints");
        Check(a.SceneAt(14).Audio[0].Gain == 0.2m && a.SceneAt(15).Audio[0].Gain == 0.6m, "Audio gain step evaluated by SceneAt");
        foreach (var (key, value) in new[] { ("width", "0"), ("height", "-1"), ("scale", "-1"), ("opacity", "1.1"), ("cropX", "0.9"), ("cropWidth", "0"), ("anchorX", "2") })
            Reject(() => Compile(Mutate(n => n["shapes"]![0]!["transform"]![key] = JsonNode.Parse(value))), $"Invalid {key} rejected");
        Reject(() => Compile(Mutate(n => n["shapes"]![0]!["transform"]!["x"] = "1 / (frame - 15)")), "Interior-frame division by zero rejected at compile");
        Reject(() => Compile(Mutate(n => n["shapes"]![0]!["transform"]!["x"]!["keys"]![1]!["frame"] = 0)), "Unordered keyframes rejected");
        Reject(() => Compile(Mutate(n => n["shapes"]![0]!["transform"]!["filter"] = "rotate")), "Raw backend property rejected");
        Reject(() => Compile(Mutate(n => n["shapes"]![0]!["unexpected"] = 1)), "Unknown JSON member rejected");
        Reject(() => Compile(source.Replace("\"fps\": 30", "\"fps\": 30, \"fps\": 25")), "Duplicate JSON member rejected");
        Reject(() => Compile(Mutate(n => n["frames"] = 601)), "Programmable frame limit");
        Reject(() => Compile(Mutate(n => { var items = new JsonArray(); for (int i = 0; i < 65; i++) items.Add(n["shapes"]![0]!.DeepClone()); n["shapes"] = items; })), "Layer count limit");
        Reject(() => Compile(Mutate(n => { var keys = new JsonArray(); for (int i = 0; i < 65; i++) keys.Add(new JsonObject { ["frame"] = i, ["value"] = 1 }); n["shapes"]![0]!["transform"]!["x"] = new JsonObject { ["keys"] = keys }; })), "Keyframe count limit");
        Reject(() => Compile(Mutate(n => n["shapes"]![0]!["when"] = "1")), "Numeric condition is not coerced to boolean");
        var sensitive = Compile(Mutate(n => n["shapes"]![0]!["transform"]!["opacity"] = "startX / 20"));
        var sr = sensitive.CreateRuntime(); Reject(() => sr.SetMany(new Dictionary<string, decimal> { ["startX"] = 60 }), "Invalid bound expression rejected before publication");
        Check(sr.Bind().Values["startX"] == 20, "Semantic validation preserves atomic SetMany");
        var conditional = Compile(File.ReadAllText("examples/conditional.json")); var cr = conditional.CreateRuntime(); var ca = cr.Bind();
        cr.SetMany(new Dictionary<string, decimal> { ["score"] = 10 }); var cb = cr.Bind();
        Check(ca.SceneAt(0).Layers[0].Included && !cb.SceneAt(0).Layers[0].Included && !ca.SceneAt(0).Layers[1].Included && ca.SceneAt(29).Layers[1].Included, "Parameter and progress conditions with snapshot isolation");
        Check(ca.SceneAt(0).Layers.Select(l => l.ZOrder).SequenceEqual([0, 1]), "Excluded layers remain inspectable in stable z-order");
        var effectSource = File.ReadAllText("examples/effects.json"); var effects = Compile(effectSource).CreateRuntime().Bind();
        Check(effects.Composition.Effects.Count == 4 && effects.SceneAt(29).Layers[0].Transform.ScaleX == 2.5m && effects.SceneAt(29).Layers[1].Transform.Opacity == 1 && effects.SceneAt(29).Layers[2].Transform.X == 110 && effects.SceneAt(15).Layers[3].Transform.ScaleX == 1.8m, "Zoom, fade, slide and pulse effects");
        var reuseNode = JsonNode.Parse(effectSource)!; reuseNode["shapes"]![1]!["effects"] = JsonNode.Parse("[{\"name\":\"zoom\",\"arguments\":{\"amount\":1.2}}]");
        var reused = Compile(reuseNode.ToJsonString()).CreateRuntime().Bind();
        Check(reused.SceneAt(29).Layers[0].Transform.ScaleX == 2.5m && reused.SceneAt(29).Layers[1].Transform.ScaleX == 1.2m, "Effect invocations have independent arguments");
        Reject(() => Compile(effectSource.Replace("\"amount\": 2.5", "\"typo\": 2.5")), "Unknown effect argument rejected");
        Reject(() => Compile(effectSource.Replace("\"name\": \"zoom\"", "\"name\": \"absent\"")), "Unknown effect invocation rejected");
        Reject(() => Compile(effectSource.Replace("\"parameters\": { \"amount\": 2 }", "\"effects\": [], \"parameters\": { \"amount\": 2 }")), "Recursive effect schema forbidden");
        var data = Compile(File.ReadAllText("examples/data-sequence.json"));
        Check(data.Clips.Length == 2 && data.Clips[1].At == 12 && data.CreateRuntime().Bind().SceneAt(15).Layers.Length == 2, "Data records expand into ordered trimmed sequence with fade");
        var dataNode = JsonNode.Parse(File.ReadAllText("examples/data-sequence.json"))!;
        var manyRecords = new JsonArray(); for (int i = 0; i < 129; i++) manyRecords.Add(dataNode["data"]!["highlights"]![0]!.DeepClone());
        dataNode["data"]!["highlights"] = manyRecords;
        Reject(() => Compile(dataNode.ToJsonString()), "Data record bound");
        var generatedNode = JsonNode.Parse(File.ReadAllText("examples/data-sequence.json"))!;
        var repeatedRecords = new JsonArray(); for (int i = 0; i < 65; i++) repeatedRecords.Add(generatedNode["data"]!["highlights"]![0]!.DeepClone());
        generatedNode["data"]!["highlights"] = repeatedRecords;
        generatedNode["sequences"]!.AsArray().Add(generatedNode["sequences"]![0]!.DeepClone());
        Reject(() => Compile(generatedNode.ToJsonString()), "Generated element bound");
        var effectLimitNode = JsonNode.Parse(effectSource)!;
        for (int i = 0; i < 33; i++) effectLimitNode["effects"]![$"copy{i}"] = effectLimitNode["effects"]!["zoom"]!.DeepClone();
        Reject(() => Compile(effectLimitNode.ToJsonString()), "Effect definition bound");
        var batch = Batches.Bind(Compile(File.ReadAllText("examples/expressions.json")), File.ReadAllText("examples/batch.json"), directory);
        Check(batch.Length == 2 && ReferenceEquals(batch[0].Snapshot.Composition, batch[1].Snapshot.Composition) && batch[0].Snapshot.Values["offset"] == 0 && batch[1].Snapshot.Values["offset"] == 20, "Batch shares structure and isolates bindings");
        Reject(() => Batches.Bind(batch[0].Snapshot.Composition, "[{\"output\":\"a.mp4\",\"parameters\":{}},{\"output\":\"a.mp4\",\"parameters\":{}}]", directory), "Duplicate batch destination rejected before rendering");
        var plan = Ffmpeg.Plan(a, "proof.mp4");
        Check(plan.Expected.Frames == 30 && plan.Expected.SampleRate == 48000 && plan.Arguments.Contains("libx264") && plan.Inputs.Length == 1 && plan.FilterGraph.Contains("rotate=") && plan.FilterGraph.Contains("crop=") && !plan.FilterGraph.Contains("startX") && !plan.FilterGraph.Contains("progress"), "Plan owns settings and fully lowers expressions to numbers");
        Check(plan.FilterGraph == Ffmpeg.Plan(a, "proof.mp4").FilterGraph, "Repeated plan is identical");
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        try { System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR"); Check(plan.FilterGraph == Ffmpeg.Plan(a, "proof.mp4").FilterGraph && E("0.25 + 0.5") == 0.75m, "Invariant parsing and lowering under non-English culture"); }
        finally { System.Globalization.CultureInfo.CurrentCulture = originalCulture; }
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var extension in new[] { "mp4", "webm" })
        {
            var output = $"artifacts/programmable.{extension}"; var repeat = $"artifacts/programmable-repeat.{extension}";
            Console.WriteLine($"Rendering programmable transforms and gain twice as {extension}...");
            await Ffmpeg.Render(a, output); await Ffmpeg.Render(a, repeat);
            Check(Ffmpeg.Hash(output) == Ffmpeg.Hash(repeat), $"Programmable {extension} byte-identical repeat"); hashes[extension] = Ffmpeg.Hash(output);
            var probe = await Ffmpeg.Run("ffprobe", ["-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames", "-of", "json", output]);
            using var doc = JsonDocument.Parse(probe);
            Check(doc.RootElement.GetProperty("streams")[0].GetProperty("nb_read_frames").GetString() == "30", $"Programmable {extension} has exactly 30 frames");
            await Ffmpeg.Run("ffmpeg", ["-v", "error", "-xerror", "-i", output, "-f", "null", "-"]);
        }
        async Task<byte[]> Pixels(string input, int frame, string name)
        {
            var output = $"artifacts/{name}.rgb";
            await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", input, "-vf", $"select=eq(n\\,{frame})", "-frames:v", "1", "-pix_fmt", "rgb24", "-f", "rawvideo", output]); return File.ReadAllBytes(output);
        }
        var first = await Pixels("artifacts/programmable.mp4", 0, "program-first"); var last = await Pixels("artifacts/programmable.mp4", 29, "program-last");
        Check(first[(30 * 160 + 20) * 3 + 1] > 200 && first[(30 * 160 + 50) * 3 + 1] < 15, "Decoded initial anchor position agrees with SceneAt");
        Check(last[(60 * 160 + 120) * 3 + 1] is > 100 and < 155 && last[(78 * 160 + 120) * 3 + 1] > 90 && last[(60 * 160 + 138) * 3 + 1] < 15, "Decoded 90-degree rotation, scale and opacity agree with SceneAt");
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", "artifacts/programmable.mp4", "-vn", "-ac", "1", "-ar", "48000", "-f", "s16le", "artifacts/program-audio.pcm"]);
        var samples = File.ReadAllBytes("artifacts/program-audio.pcm");
        double Rms(int start) { double sum = 0; for (int i = start; i < start + 9600; i++) { double n = BitConverter.ToInt16(samples, i * 2); sum += n * n; } return Math.Sqrt(sum / 9600); }
        double ratio = Rms(30000) / Rms(4800);
        Check(ratio is > 2.8 and < 3.2, "Decoded animated gain matches SceneAt step ratio");
        await Ffmpeg.Render(ca, "artifacts/conditional-on.mp4"); await Ffmpeg.Render(cb, "artifacts/conditional-off.mp4");
        var on = await Pixels("artifacts/conditional-on.mp4", 0, "on"); var off = await Pixels("artifacts/conditional-off.mp4", 0, "off");
        Check(on[(15 * 160 + 20) * 3 + 1] > 200 && off[(15 * 160 + 20) * 3 + 1] < 15, "Decoded conditional inclusion follows frozen bindings");
        // A two-color source makes crop errors visible; a solid source cannot prove crop.
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i", "color=red:s=160x96:r=30:d=1,drawbox=x=80:y=0:w=80:h=96:color=blue:t=fill", "-c:v", "libx264", "-threads", "1", "-crf", "0", "artifacts/assets/crop.mp4"]);
        const string cropSource = """
        {"width":160,"height":96,"fps":30,"frames":3,"parameters":{},
         "videos":[{"asset":"../artifacts/assets/crop.mp4","trim":0,"frames":3,"transform":{"cropX":0.5,"cropWidth":0.5,"width":40,"height":20,"scaleX":2,"scaleY":1,"rotation":90,"anchorX":0.5,"anchorY":0.5,"x":80,"y":48}}],
         "audio":[],"shapes":[{"at":0,"frames":3,"width":8,"height":8,"color":"00FF00","transform":{"x":76,"y":44}}]}
        """;
        var cropSnapshot = Compile(cropSource).CreateRuntime().Bind(); await Ffmpeg.Render(cropSnapshot, "artifacts/crop-rotation.mp4");
        var cropPixels = await Pixels("artifacts/crop-rotation.mp4", 0, "crop");
        Check(cropPixels[(20 * 160 + 80) * 3 + 2] > 200 && cropPixels[(20 * 160 + 80) * 3] < 20 && cropPixels[(48 * 160 + 100) * 3 + 2] < 20, "Decoded normalized crop, nonuniform scale and anchor rotation");
        Check(cropPixels[(48 * 160 + 80) * 3 + 1] > 200 && cropSnapshot.SceneAt(0).Layers[1].ZOrder == 1, "Decoded upper shape respects scene layer order");
        var audioCondition = Compile(Mutate(n => n["audio"]![0]!["when"] = "frame < 15")).CreateRuntime().Bind();
        Check(audioCondition.SceneAt(14).Audio[0].Included && !audioCondition.SceneAt(15).Audio[0].Included, "Audio inclusion has explicit local-frame semantics");
        await Ffmpeg.Render(data.CreateRuntime().Bind(), "artifacts/data-sequence.mp4");
        var dataPixels = await Pixels("artifacts/data-sequence.mp4", 15, "data-middle");
        Check(dataPixels[(48 * 160 + 80) * 3] > 70 && dataPixels[(48 * 160 + 80) * 3 + 2] > 70, "Generated clip transform and crossfade render correctly");
        foreach (var item in batch) await Ffmpeg.Render(item.Snapshot, item.Output);
        await Ffmpeg.Render(effects, "artifacts/effects.mp4");
        File.WriteAllText("artifacts/programmable.filter.txt", plan.FilterGraph);
        File.WriteAllText("artifacts/programmable-proof.json", JsonSerializer.Serialize(new { checks, hashes, gainRatio = ratio }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: {checks.Count} programmable checks, plus 29 legacy checks.");
    }
}
