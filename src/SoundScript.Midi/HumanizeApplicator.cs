namespace SoundScript.Midi;

/// <summary>Applies micro-timing humanization to note start times before MIDI emission.</summary>
internal static class HumanizeApplicator
{
    private static Func<double, double>? _offsetFactory;

    internal static void SetOffsetFactory(Func<double, double>? factory) => _offsetFactory = factory;

    internal static double SampleOffsetSeconds(double humanizeSeconds)
    {
        if (humanizeSeconds <= 0)
            return 0;

        if (_offsetFactory is not null)
            return _offsetFactory(humanizeSeconds);

        return (Random.Shared.NextDouble() * 2 - 1) * humanizeSeconds;
    }

    internal static double ApplyToStartBeat(double startBeat, double humanizeSeconds, int tempoBpm)
    {
        var offsetSeconds = SampleOffsetSeconds(humanizeSeconds);
        if (offsetSeconds == 0)
            return startBeat;

        var offsetBeats = offsetSeconds * tempoBpm / 60.0;
        return Math.Max(0, startBeat + offsetBeats);
    }
}
