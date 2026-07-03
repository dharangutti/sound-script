namespace SoundScript.Midi;

/// <summary>Deterministic micro-timing and micro-velocity humanization before MIDI emission.</summary>
internal static class HumanizeApplicator
{
    private const int DefaultSeed = 1337;

    private static int? _seedOverride;

    internal static void SetSeed(int? seed) => _seedOverride = seed;

    internal static double SampleOffsetSeconds(double humanizeSeconds, int noteIndex, byte channel = 0)
    {
        if (humanizeSeconds <= 0)
            return 0;

        return SampleUnit(noteIndex, channel) * humanizeSeconds;
    }

    internal static int ApplyVelocity(int velocity, double humanize, int noteIndex, byte channel = 0)
    {
        if (humanize <= 0)
            return velocity;

        var jitter = (int)Math.Round(SampleUnit(noteIndex, channel) * humanize * 127);
        return Math.Clamp(velocity + jitter, 1, 127);
    }

    internal static double ApplyToStartBeat(
        double startBeat,
        double humanizeSeconds,
        int tempoBpm,
        int noteIndex,
        byte channel = 0)
    {
        var offsetSeconds = SampleOffsetSeconds(humanizeSeconds, noteIndex, channel);
        if (offsetSeconds == 0)
            return startBeat;

        var offsetBeats = offsetSeconds * tempoBpm / 60.0;
        return Math.Max(0, startBeat + offsetBeats);
    }

    private static double SampleUnit(int noteIndex, byte channel)
    {
        var seed = HashCode.Combine(_seedOverride ?? DefaultSeed, noteIndex, channel);
        var random = new Random(seed);
        return random.NextDouble() * 2 - 1;
    }
}
