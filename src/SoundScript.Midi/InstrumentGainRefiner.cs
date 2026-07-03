namespace SoundScript.Midi;

/// <summary>Second-stage instrument gain normalization before MIDI output.</summary>
internal static class InstrumentGainRefiner
{
    private static readonly HashSet<string> PercussiveInstruments = new(StringComparer.OrdinalIgnoreCase)
    {
        "piano",
        "trumpet",
        "guitar"
    };

    internal static (int Velocity, bool Refined) Apply(string? instrumentName, int velocity)
    {
        var refined = velocity;
        var changed = false;

        if (velocity < 40)
        {
            refined = Math.Min(127, (int)Math.Round(velocity * 1.08));
            changed = true;
        }
        else if (velocity > 110)
        {
            refined = Math.Max(1, (int)Math.Round(velocity * 0.95));
            changed = true;
        }

        if (instrumentName is not null && PercussiveInstruments.Contains(instrumentName))
        {
            var normalized = refined / 127.0;
            var compressed = 0.82 * normalized + 0.09;
            refined = Math.Clamp((int)Math.Round(compressed * 127.0), 1, 127);
            changed = true;
        }

        return (refined, changed);
    }
}
