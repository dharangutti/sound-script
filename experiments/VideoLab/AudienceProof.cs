using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoLab;

internal static class AudienceProof
{
    internal static async Task Run()
    {
        var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL: " + name); checks.Add(name); }
        void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is ArgumentException or JsonException) { checks.Add(name); return; } throw new InvalidOperationException("FAIL: " + name); }
        var source = File.ReadAllText("examples/audience.json");
        var directory = Path.GetFullPath("examples");
        Composition Compile(string text) => Composition.Compile(text, directory);
        string Change(Action<JsonNode> edit) { var node = JsonNode.Parse(source)!; edit(node); return node.ToJsonString(); }
        var legacyBooleanName = Compile(Change(n => { n["parameters"]!["true"] = JsonNode.Parse("{\"default\":1,\"min\":0,\"max\":1}"); n["texts"]![0]!["transform"]!["opacity"] = "true"; n["texts"]![6]!["when"] = "showSafety"; }));
        Check(legacyBooleanName.CreateRuntime().Bind().SceneAt(0).Layers.First(l => l.Kind == "text").Transform.Opacity == 1, "Legacy numeric parameter named true remains numeric");
        var c = Compile(source); var runtime = c.CreateRuntime(); var original = runtime.Bind();
        Dictionary<string, JsonElement> Bind(string audience, bool safety = true) => new() { ["audience"] = JsonSerializer.SerializeToElement(audience), ["showSafety"] = JsonSerializer.SerializeToElement(safety) };
        foreach (var audience in new[] { "shopfloor", "qa", "engineering" })
        {
            runtime.SetBindings(Bind(audience)); var snapshot = runtime.Bind();
            var scene = snapshot.SceneAt(30);
            Check(scene.Layers.Where(l => l.Group != null && l.Included).All(l => l.Group == audience), "Only intended audience group included: " + audience);
            Check(scene.Layers.Count(l => l.Kind == "callout" && l.Included) == 1 && scene.Layers.Count(l => l.Kind == "text" && l.Included) >= 2, "Useful title/instruction/callout: " + audience);
            Check(scene.Clips.SequenceEqual(original.SceneAt(30).Clips), "Identical underlying footage: " + audience);
            Check(ReferenceEquals(c, snapshot.Composition), "Compiled structure reused: " + audience);
        }
        Check(original.TypedValues["audience"].GetString() == "shopfloor", "Earlier snapshot remains frozen");
        runtime.SetBindings(Bind("shopfloor", false));
        Check(!runtime.Bind().SceneAt(30).Layers.Single(l => l.Source.StartsWith("SAFETY:")).Included, "Boolean combines with group predicate");
        var before = JsonSerializer.Serialize(runtime.Bind().Bindings);
        Reject(() => runtime.SetBindings(new Dictionary<string, JsonElement> { ["musicGain"] = JsonSerializer.SerializeToElement(.8), ["audience"] = JsonSerializer.SerializeToElement("unknown") }), "Mixed invalid transaction rejected");
        Check(JsonSerializer.Serialize(runtime.Bind().Bindings) == before, "Failed mixed transaction changes neither numeric nor typed state");
        foreach (var value in new[] { "1", "null", "true", "\"QA\"", "{}" })
            Reject(() => runtime.SetBindings(new Dictionary<string, JsonElement> { ["audience"] = JsonSerializer.Deserialize<JsonElement>(value) }), "Invalid enum JSON " + value);
        Reject(() => runtime.SetBindings(new Dictionary<string, JsonElement> { ["showSafety"] = JsonSerializer.SerializeToElement("true") }), "Boolean is not a string");
        foreach (var condition in new[] { "audience == 'missing'", "audience == 1", "audience > 'qa'", "showSafety + 1", "showSafety == 'qa'", "'qa' == 'qa'", "audience == 'qa' && 1", "host.run()" })
            Reject(() => Compile(Change(n => n["groups"]!["qa"]!["when"] = condition)), "Typed AST rejects " + condition);
        Reject(() => Compile(Change(n => n["texts"]![0]!["transform"]!["x"] = "audience")), "Enum cannot drive numeric transform");
        Reject(() => Compile(Change(n => n["texts"]![0]!["group"] = "missing")), "Unknown group rejected");
        Reject(() => Compile(Change(n => n["typedParameters"]!["audience"]!["values"] = JsonNode.Parse("[\"qa\",\"qa\"]"))), "Duplicate enum values rejected");
        Reject(() => Compile(Change(n => n["typedParameters"]!["audience"]!["type"] = "string")), "Unbounded runtime strings rejected");
        Reject(() => Compile(Change(n => n["typedParameters"]!["musicGain"] = n["typedParameters"]!["showSafety"]!.DeepClone())), "Parameter namespace collision rejected");
        var boolExpression = Compile(Change(n => n["groups"]!["qa"]!["when"] = "(audience == 'qa' || audience == 'engineering') && showSafety"));
        var br = boolExpression.CreateRuntime(); br.SetBindings(Bind("engineering"));
        Check(br.Bind().SceneAt(0).Layers.Any(l => l.Group == "qa" && l.Included), "Typed logical precedence and parentheses");
        var groupedAudio = Compile(Change(n => n["audio"]![0]!["group"] = "qa"));
        Check(!groupedAudio.CreateRuntime().Bind().SceneAt(0).Audio[0].Included, "Groups control audio too");
        var batch = Batches.Bind(c, File.ReadAllText("examples/audience-batch.json"), directory);
        Check(batch.Length == 3 && batch.All(r => ReferenceEquals(r.Snapshot.Composition, c)), "Audience batch shares one compilation");
        var root = Path.GetFullPath("artifacts/audience-proof"); Directory.CreateDirectory(root);
        var hashes = new List<string>(); var decoded = new List<string>();
        foreach (var render in batch)
        {
            var audience = render.Snapshot.TypedValues["audience"].GetString();
            foreach (var format in new[] { "mp4", "webm" })
            {
                Console.WriteLine($"Audience proof: {audience}.{format} twice...");
                var file = Path.Combine(root, audience + "." + format); var repeat = Path.Combine(root, audience + "-repeat." + format);
                await Ffmpeg.Render(render.Snapshot, file); await Ffmpeg.Render(render.Snapshot, repeat);
                Check(Ffmpeg.Hash(file) == Ffmpeg.Hash(repeat), "Deterministic audience output: " + audience + "." + format);
                await Ffmpeg.Run("ffmpeg", ["-v", "error", "-xerror", "-i", file, "-f", "null", "-"]);
                checks.Add("Full decode: " + audience + "." + format); hashes.Add(Ffmpeg.Hash(file));
                if (format == "mp4")
                {
                    var image = Path.Combine(root, audience + ".png");
                    await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", file, "-ss", "1", "-frames:v", "1", image]); decoded.Add(Ffmpeg.Hash(image));
                }
            }
        }
        Check(hashes.Distinct().Count() == 6 && decoded.Distinct().Count() == 3, "Three different audience videos and decoded images");
        File.WriteAllText(Path.Combine(root, "report.json"), JsonSerializer.Serialize(new { checks = checks.Count, passed = checks, outputHashes = hashes }));
        Console.WriteLine($"PASS: {checks.Count} Phase 2 audience checks.");
    }
}
