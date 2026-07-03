namespace SoundScript.Midi;

/// <summary>Normalizes shaped durations to prevent cumulative timing drift.</summary>
internal static class DurationNormalizer
{
    internal static (double DurationBeats, bool Normalized) Apply(double durationBeats)
    {
        var rounded = BeatMath.RoundBeat(durationBeats);
        return (rounded, Math.Abs(rounded - durationBeats) > 1e-12);
    }
}
