using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VideoLab;

public sealed record AnnotationDocument(int SchemaVersion, ImmutableArray<Annotation> Annotations, string? AudienceParameter = null);
public sealed record Annotation(string Id, string Text, int StartFrame, int EndFrame,
    int X, int Y, int Width, int Height, string Category = "comment", string Severity = "normal",
    string Style = "text", ImmutableArray<string> Audiences = default, int? TargetX = null, int? TargetY = null);

// Pure data-to-primitives lowering. Only the CLI/batch/browser adapters read files.
public static class Annotations
{
    private static readonly ImmutableDictionary<string, string> Colors = new Dictionary<string, string>
    {
        ["instruction"] = "79DECF", ["information"] = "91B7FF", ["warning"] = "F4C95D",
        ["inspection"] = "6EE7B7", ["revision"] = "C6ADFF", ["comment"] = "D1D9E6"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static string Expand(string baseSource, string annotationSource, string directory)
    {
        var composition = Composition.Compile(baseSource, directory);
        Composition.Require(annotationSource.Length <= 100_000, "Annotation document exceeds 100,000 characters.");
        using var doc = JsonDocument.Parse(annotationSource, new JsonDocumentOptions { MaxDepth = 16 });
        JsonRules.NoDuplicates(doc.RootElement);
        var data = JsonSerializer.Deserialize<AnnotationDocument>(annotationSource, new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true });
        Composition.Require(data != null && data.SchemaVersion == 1 && !data.Annotations.IsDefault && data.Annotations.Length <= 24, "Annotation schemaVersion 1 requires an array of at most 24 records.");
        TypedParameter? audience = null;
        if (data.AudienceParameter != null)
        {
            Composition.Require(composition.Script.TypedParameters?.TryGetValue(data.AudienceParameter, out audience) == true && audience.Type == "enum", "Annotation audienceParameter must name an enum.");
        }
        var source = JsonNode.Parse(baseSource)!.AsObject();
        // Canonical property spellings avoid creating case-variant duplicates when
        // the base uses the compiler's case-insensitive property convention.
        JsonNode? Find(string name) => source.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
        JsonArray Array(string name)
        {
            if (Find(name) is JsonArray existing) return existing;
            foreach (var key in source.Select(p => p.Key).Where(k => k.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray()) source.Remove(key);
            var array = new JsonArray(); source[name] = array; return array;
        }
        var texts = Array("texts"); var callouts = Array("callouts"); var shapes = Array("shapes");
        JsonObject groups;
        if (Find("groups") is JsonObject existingGroups) groups = existingGroups;
        else
        {
            foreach (var key in source.Select(p => p.Key).Where(k => k.Equals("groups", StringComparison.OrdinalIgnoreCase)).ToArray()) source.Remove(key);
            groups = new JsonObject(); source["groups"] = groups;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in data.Annotations)
        {
            Composition.Require(a != null && TypedParameters.Identifier(a.Id) && a.Id.Length <= 48 && ids.Add(a.Id), "Annotation IDs must be unique identifiers up to 48 characters.");
            Composition.Require(a.StartFrame >= 0 && a.EndFrame > a.StartFrame && a.EndFrame <= composition.Script.Frames, $"Annotation '{a.Id}' lifetime is outside the timeline.");
            Composition.Require(Colors.ContainsKey(a.Category ?? "") && a.Severity is "normal" or "warning" or "critical", "Unknown annotation category or severity.");
            Composition.Require(a.Style is "text" or "callout" or "highlight", "Unknown annotation style.");
            Composition.Require(a.X >= 0 && a.Y >= 0 && a.Width >= 24 && a.Height >= 36 && a.Width % 2 == 0 && a.Height % 2 == 0 && (long)a.X + a.Width <= composition.Script.Width && (long)a.Y + a.Height <= composition.Script.Height, "Annotation plane must fit inside the canvas with even dimensions.");
            Composition.Require(a.Style == "callout" ? a.TargetX.HasValue && a.TargetY.HasValue : a.TargetX == null && a.TargetY == null, "Only callout annotations require targetX and targetY.");
            var color = a.Severity switch { "warning" => "F4C95D", "critical" => "FFB4A6", _ => Colors[a.Category!] };
            Captions.Validate(a.Text, a.Width, a.Height, 20, color, "left");
            var members = a.Audiences.IsDefault ? ImmutableArray<string>.Empty : a.Audiences;
            Composition.Require(members.Length <= 32 && members.Distinct(StringComparer.Ordinal).Count() == members.Length && members.All(m => audience != null && audience.Values.Contains(m, StringComparer.Ordinal)), "Annotation audiences must be unique members of the declared enum.");
            var group = "annotation_" + a.Id;
            Composition.Require(!groups.ContainsKey(group), "Annotation group collides with base composition group.");
            groups[group] = members.Length == 0 ? new JsonObject() : new JsonObject { ["when"] = string.Join(" || ", members.Select(m => $"{data.AudienceParameter} == '{m}'")) };
            var item = new JsonObject { ["at"] = a.StartFrame, ["frames"] = a.EndFrame - a.StartFrame,
                ["width"] = a.Width, ["height"] = a.Height, ["fontSize"] = 20, ["color"] = color,
                ["group"] = group, ["transform"] = new JsonObject { ["x"] = a.X, ["y"] = a.Y } };
            if (a.Style == "callout")
            {
                item["label"] = a.Text; item["background"] = "182438"; item["targetX"] = a.TargetX; item["targetY"] = a.TargetY; callouts.Add(item);
            }
            else
            {
                item["text"] = a.Text; texts.Add(item);
                if (a.Style == "highlight")
                {
                    // Four ordinary shapes form an outline; no special renderer.
                    void Edge(int x, int y, int width, int height) => shapes.Add(new JsonObject
                    { ["at"] = a.StartFrame, ["frames"] = a.EndFrame - a.StartFrame, ["width"] = width, ["height"] = height,
                        ["color"] = color, ["group"] = group, ["transform"] = new JsonObject { ["x"] = x, ["y"] = y } });
                    Edge(a.X, a.Y, a.Width, 2); Edge(a.X, a.Y + a.Height - 2, a.Width, 2);
                    Edge(a.X, a.Y, 2, a.Height); Edge(a.X + a.Width - 2, a.Y, 2, a.Height);
                }
            }
        }
        var expanded = source.ToJsonString();
        Composition.Compile(expanded, directory); // enforce aggregate bounds after expansion
        return expanded;
    }

    internal static string ExampleFile(string name) => name switch
    {
        "shopfloor" => "shopfloor-instructions.json", "qa" => "qa-comments.json", "engineering" => "engineering-review.json",
        _ => throw new ArgumentException("Unknown annotation dataset.")
    };

    public static string ReadFile(string path)
    {
        Composition.Require(new FileInfo(path).Length <= 400_000, "Annotation file exceeds 400,000 bytes.");
        return File.ReadAllText(path);
    }
}
