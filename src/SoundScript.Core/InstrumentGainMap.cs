namespace SoundScript.Core;

/// <summary>Per-instrument gain normalization applied before MIDI output.</summary>
public static class InstrumentGainMap
{
    private static readonly Dictionary<string, double> Gains = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piano"] = 1.00,
        ["flute"] = 0.85,
        ["violin"] = 0.90,
        ["bass"] = 0.75,
        ["guitar"] = 0.88,
        ["trumpet"] = 0.92,
        ["cello"] = 0.87,
        ["organ"] = 0.95,
        ["synth"] = 0.93
    };

    public static double GetGain(string? instrumentName)
    {
        if (instrumentName is not null && Gains.TryGetValue(instrumentName, out var gain))
            return gain;

        return 1.0;
    }
}
