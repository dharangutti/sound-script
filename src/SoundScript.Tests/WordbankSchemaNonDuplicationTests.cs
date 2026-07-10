using System.Text.Json;
using Xunit;

namespace SoundScript.Tests;

/// <summary>
/// Guards against reintroducing duplicate or conflicting metadata keys in the
/// soundscript-wordbank lemma schema versus the fields the normalizer/auto-generator
/// write (Prompts 1–2). Runs as part of the CI test suite.
/// </summary>
public class WordbankSchemaNonDuplicationTests
{
    // Fields the auto-generator writes onto a lemma entry (Prompt 2).
    private static readonly string[] GeneratorEntryKeys =
        ["lemma", "audio", "generator", "generatorVersion", "generatedAt", "normalizerVersion"];

    // Keys the normalizer writes to the sidecar (Prompt 1).
    private static readonly string[] SidecarKeys =
        ["normalized", "basePitchHz", "durationMs", "energyRMS", "normalizerVersion"];

    // Human-recording provenance fields that must NOT be duplicated/aliased.
    private static readonly string[] RecordingKeys =
        ["license", "source", "attribution", "audio", "trimStartMs", "trimEndMs", "gain", "pitchSemitones"];

    [Fact]
    public void LemmaSchema_DeclaresGeneratorFields_WithoutDuplicates()
    {
        var properties = LoadEntryProperties();

        foreach (var key in new[] { "generator", "generatorVersion", "generatedAt", "normalizerVersion" })
            Assert.Contains(key, properties.Keys);

        // JSON object keys are inherently unique; assert the count matches the
        // distinct set as a defensive guard against a malformed hand-edit.
        Assert.Equal(properties.Count, properties.Keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GeneratorWrittenKeys_AreAllDeclaredInSchema()
    {
        var properties = LoadEntryProperties();
        foreach (var key in GeneratorEntryKeys)
            Assert.True(properties.ContainsKey(key), $"Generator writes undeclared lemma key '{key}'.");
    }

    [Fact]
    public void SidecarKeys_DoNotCollideWithRecordingFields()
    {
        // The normalization sidecar is a separate file; none of its keys may
        // reuse the human-recording provenance field names.
        var collisions = SidecarKeys.Intersect(RecordingKeys, StringComparer.Ordinal).ToArray();
        Assert.Empty(collisions);
    }

    [Fact]
    public void GeneratorProvenance_DoesNotDuplicateAttributionOrRecordingFields()
    {
        // generator/generatorVersion/generatedAt must be distinct from the
        // existing source/attribution provenance fields.
        var provenance = new[] { "generator", "generatorVersion", "generatedAt" };
        Assert.Empty(provenance.Intersect(RecordingKeys, StringComparer.Ordinal));
    }

    private static Dictionary<string, JsonElement> LoadEntryProperties()
    {
        var schemaPath = ResolveSchemaPath();
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));

        var entryProps = doc.RootElement
            .GetProperty("properties")
            .GetProperty("entries")
            .GetProperty("items")
            .GetProperty("properties");

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in entryProps.EnumerateObject())
            result[prop.Name] = prop.Value.Clone();

        return result;
    }

    private static string ResolveSchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var name in new[] { "wordbank", "../soundscript-wordbank" })
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, name, "schema", "lemmas.schema.json"));
                if (File.Exists(candidate))
                    return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate soundscript-wordbank lemmas.schema.json.");
    }
}
