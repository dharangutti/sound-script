namespace SoundScript.Core.Notation;

/// <summary>Standard note durations in quarter-note beats.</summary>
public enum NoteDuration
{
    Quarter,
    Half,
    Eighth,
    Whole
}

public static class NoteDurationExtensions
{
    public static double ToBeats(this NoteDuration duration) => duration switch
    {
        NoteDuration.Quarter => 1.0,
        NoteDuration.Half => 2.0,
        NoteDuration.Eighth => 0.5,
        NoteDuration.Whole => 4.0,
        _ => throw new ArgumentOutOfRangeException(nameof(duration))
    };
}
