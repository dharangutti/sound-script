using System.Text.Json;

namespace SoundScript.Media;

/// <summary>Serializes typed scenes as deterministic versioned JSON without exposing syntax trees.</summary>
public static class TemporalVisualJson
{
    /// <summary>The wire schema emitted by this adapter.</summary>
    public const string SchemaVersion = "1.0";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Serializes a scene in source layer order using invariant numbers and safely escaped strings.</summary>
    /// <remarks>Paths contain absolute, already rotated logical viewport coordinates. Null paths denote legacy presentation.</remarks>
    public static string Serialize(TemporalVisualScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, timeSeconds = scene.TimeSeconds, primitives = scene.Primitives }, Options);
    }
}
