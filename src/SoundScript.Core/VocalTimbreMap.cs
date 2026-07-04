namespace SoundScript.Core;

/// <summary>
/// Maps vocal timbre names to General MIDI voice programs.
/// Parallel to <see cref="InstrumentMap"/> but scoped to voice blocks only.
/// </summary>
public static class VocalTimbreMap
{
    /// <summary>General MIDI program 52 — Choir Aahs.</summary>
    public const int DefaultProgram = 52;

    private static readonly Dictionary<string, int> Programs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["choir"] = 52,
        ["aahs"] = 52,
        ["oohs"] = 53,
        ["synthvoice"] = 54
    };

    public static int Resolve(string name)
    {
        if (Programs.TryGetValue(name, out var program))
            return program;

        throw new InvalidOperationException(
            $"Unknown vocal timbre '{name}'. Supported: choir, aahs, oohs, synthvoice.");
    }
}
