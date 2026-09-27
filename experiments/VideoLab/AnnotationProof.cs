using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoLab;

internal static class AnnotationProof
{
    internal static async Task Run()
    {
        var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL: " + name); checks.Add(name); }
        void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is ArgumentException or JsonException or IOException) { checks.Add(name); return; } throw new InvalidOperationException("FAIL: " + name); }
        var source = File.ReadAllText("examples/annotations.json"); var directory = Path.GetFullPath("examples");
        var data = File.ReadAllText("examples/annotations/qa-comments.json");
        string Expand(string json) => Annotations.Expand(source, json, directory);
        string Change(Action<JsonNode> change) { var node = JsonNode.Parse(data)!; change(node); return node.ToJsonString(); }
        var lowered = Expand(data); var c = Composition.Compile(lowered, directory); var runtime = c.CreateRuntime(); var a = runtime.Bind();
        Check(lowered == Expand(data), "Pure annotation lowering is stable");
        Check(source == File.ReadAllText("examples/annotations.json"), "Base source is unchanged");
        Check(c.Script.Shapes.Length == 4 && c.Script.Callouts.Length == 1 && c.Script.Texts.Length == 3, "Data lowers to ordinary text/callout/outline primitives");
        Check(a.SceneAt(5).Layers.All(l => l.Kind != "callout") && a.SceneAt(6).Layers.Any(l => l.Kind == "callout"), "Annotation start is inclusive");
        var shortData = Change(n => n["annotations"]![1]!["endFrame"] = 30);
        var shortScene = Composition.Compile(Expand(shortData), directory).CreateRuntime().Bind();
        Check(shortScene.SceneAt(29).Layers.Any(l => l.Kind == "callout") && shortScene.SceneAt(30).Layers.All(l => l.Kind != "callout"), "Annotation end is exclusive");
        runtime.SetBindings(new Dictionary<string, JsonElement> { ["audience"] = JsonSerializer.SerializeToElement("shopfloor") });
        Check(runtime.Bind().SceneAt(30).Layers.Where(l => l.Group != null).All(l => !l.Included) && a.SceneAt(30).Layers.Any(l => l.Group != null && l.Included), "Audience filtering preserves frozen snapshots");
        foreach (var (key, value) in new (string, JsonNode?)[] {
            ("id", JsonValue.Create("bad id")), ("text", JsonValue.Create("")), ("text", JsonValue.Create("non-ASCII \u2603")),
            ("startFrame",JsonValue.Create(-1)), ("endFrame",JsonValue.Create(61)), ("endFrame",JsonValue.Create(0)),
            ("x",JsonValue.Create(-1)), ("y",JsonValue.Create(269)), ("width",JsonValue.Create(25)), ("height",JsonValue.Create(10)),
            ("category",JsonValue.Create("script")), ("severity",JsonValue.Create("fatal")), ("style",JsonValue.Create("html")),
            ("targetX",JsonValue.Create(10)), ("audiences",JsonNode.Parse("[\"unknown\"]")), ("audiences",JsonNode.Parse("[\"qa\",\"qa\"]")), ("execute",JsonValue.Create("host.run()")) })
            Reject(() => Expand(Change(n => n["annotations"]![0]![key] = value?.DeepClone())), "Invalid annotation " + key + " " + value);
        Reject(() => Expand(Change(n => n["schemaVersion"] = 2)), "Unknown annotation version rejected");
        Reject(() => Expand(Change(n => n["annotations"]![1]!["id"] = "qa_note")), "Duplicate IDs rejected");
        Reject(() => Expand(Change(n => n["annotations"]![1]!["targetY"] = 0)), "Callout target is validated by core compiler");
        Reject(() => Expand(Change(n => n["annotations"]![1]!.AsObject().Remove("targetX"))), "Callout requires both target coordinates");
        Reject(() => Expand(Change(n => n["audienceParameter"] = "musicGain")), "Audience parameter must be an enum");
        Reject(() => Expand(Change(n => n.AsObject().Remove("audienceParameter"))), "Audience filtering requires declaration");
        Reject(() => Expand(data.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")), "Duplicate document fields rejected");
        Reject(() => Expand(Change(n => n["annotations"] = new JsonArray(Enumerable.Range(0,25).Select(i => {var row=n["annotations"]![0]!.DeepClone();row["id"]="n"+i;return row;}).ToArray()))), "Record count bounded");
        foreach (var category in new[] { "instruction", "information", "warning", "inspection", "revision", "comment" })
            Check(Expand(Change(n => n["annotations"]![0]!["category"] = category)).Length > 0, "Supported category " + category);
        var overBudget = Change(n => n["annotations"] = new JsonArray(Enumerable.Range(0,14).Select(i => {var row=n["annotations"]![2]!.DeepClone();row["id"]="h"+i;return row;}).ToArray()));
        Reject(() => Expand(overBudget), "Generated primitives retain global element cap");
        var root = Path.GetFullPath("artifacts/annotation-proof"); Directory.CreateDirectory(root);
        var baseComposition = Composition.Compile(source, directory);
        var batch = Batches.Bind(baseComposition, File.ReadAllText("examples/annotations-batch.json"), directory);
        Check(batch.Length == 3 && batch.All(r => r.Snapshot.SceneAt(30).Clips.SequenceEqual(a.SceneAt(30).Clips)), "Three datasets share identical base media");
        var repeatedData = "[{\"output\":\"../artifacts/review1.mp4\",\"annotations\":\"annotations/qa-comments.json\",\"parameters\":{\"audience\":\"qa\"}},{\"output\":\"../artifacts/review2.mp4\",\"annotations\":\"annotations/qa-comments.json\",\"parameters\":{\"audience\":\"engineering\"}}]";
        var reused = Batches.Bind(baseComposition, repeatedData, directory);
        Check(ReferenceEquals(reused[0].Snapshot.Composition, reused[1].Snapshot.Composition), "Same annotation dataset shares one compiled structure");
        var protectedFile = Path.Combine(root,"data.mp4"); File.WriteAllText(protectedFile,data);
        Reject(() => Batches.Bind(baseComposition, JsonSerializer.Serialize(new[] { new { output = protectedFile, annotations = protectedFile, parameters = new { audience = "qa" } } }), directory), "Batch cannot overwrite annotation input");
        Check(File.ReadAllText(protectedFile) == data, "Rejected output preserves dataset bytes");
        var images = new List<string>();
        foreach (var render in batch)
        {
            var audience = render.Snapshot.TypedValues["audience"].GetString();
            foreach (var format in new[] { "mp4", "webm" })
            {
                Console.WriteLine($"Annotation proof: {audience}.{format} twice...");
                var file = Path.Combine(root,audience+"."+format);var repeat=Path.Combine(root,audience+"-repeat."+format);
                await Ffmpeg.Render(render.Snapshot,file);await Ffmpeg.Render(render.Snapshot,repeat);
                Check(Ffmpeg.Hash(file)==Ffmpeg.Hash(repeat),"Repeated dataset output: "+audience+"."+format);
                await Ffmpeg.Run("ffmpeg",["-v","error","-xerror","-i",file,"-f","null","-"]);checks.Add("Decoded dataset output: "+audience+"."+format);
                if(format=="mp4") {var png=Path.Combine(root,audience+".png");await Ffmpeg.Run("ffmpeg",["-v","error","-y","-i",file,"-ss","1","-frames:v","1",png]);images.Add(Ffmpeg.Hash(png));}
            }
        }
        Check(images.Distinct().Count()==3,"Three annotation families have different decoded imagery");
        File.WriteAllText(Path.Combine(root,"report.json"),JsonSerializer.Serialize(new{checks=checks.Count,passed=checks}));
        Console.WriteLine($"PASS: {checks.Count} Phase 3 annotation checks.");
    }
}
