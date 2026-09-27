using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoLab;

public sealed record BatchRecord(string Output, ImmutableDictionary<string, JsonElement> Parameters, string? Annotations = null);
public sealed record BoundRender(string Output, Snapshot Snapshot);
public static class Batches
{
    public static ImmutableArray<BoundRender> Bind(Composition composition, string json, string directory)
    {
        Composition.Require(json.Length <= 100000, "Batch exceeds 100,000 characters.");
        using var doc = JsonDocument.Parse(json); JsonRules.NoDuplicates(doc.RootElement);
        var records = JsonSerializer.Deserialize<ImmutableArray<BatchRecord>>(json, new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true });
        Composition.Require(!records.IsDefault && records.Length is > 0 and <= 32, "Batch requires 1–32 records.");
        var result = ImmutableArray.CreateBuilder<BoundRender>(); var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var annotationFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var compiled = new Dictionary<string, Composition>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            Composition.Require(record != null && !string.IsNullOrWhiteSpace(record.Output) && record.Parameters != null, "Invalid batch record.");
            var output = Path.GetFullPath(record.Output, directory);
            Composition.Require(outputs.Add(output), "Duplicate batch output.");
            var selected = composition;
            if (record.Annotations != null)
            {
                Composition.Require(!string.IsNullOrWhiteSpace(record.Annotations), "Annotation path cannot be empty.");
                var annotationPath = Path.GetFullPath(record.Annotations, directory); annotationFiles.Add(annotationPath);
                var data = Annotations.ReadFile(annotationPath);
                if (!compiled.TryGetValue(data, out selected))
                {
                    selected = Composition.Compile(Annotations.Expand(composition.Source, data, composition.AssetDirectory), composition.AssetDirectory);
                    compiled[data] = selected;
                }
            }
            var runtime = selected.CreateRuntime(); runtime.SetBindings(record.Parameters);
            var snapshot = runtime.Bind(); var plan = Ffmpeg.Plan(snapshot, output);
            Composition.Require(!plan.Inputs.Contains(output, StringComparer.OrdinalIgnoreCase), "Batch output cannot replace an input.");
            result.Add(new(output, snapshot));
        }
        // Validate every dataset before rendering. One output must not destroy
        // another record's dataset or media, even if its own inputs differ.
        var inputs = result.SelectMany(r => Ffmpeg.Plan(r.Snapshot, r.Output).Inputs).Concat(annotationFiles).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Composition.Require(result.All(r => !inputs.Contains(r.Output)), "Batch output cannot replace any media or annotation input.");
        return result.ToImmutable();
    }
    public static async Task Render(Composition composition, string json, string directory, IEnumerable<string>? protectedPaths = null)
    {
        var renders = Bind(composition, json, directory);
        var protectedFiles = (protectedPaths ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Composition.Require(renders.All(r => !protectedFiles.Contains(r.Output)), "Batch output cannot replace its script or batch file.");
        foreach (var render in renders) await Ffmpeg.Render(render.Snapshot, render.Output);
    }
}
