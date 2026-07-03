namespace SoundScript.Core.Notation;

/// <summary>Silent notation event that advances musical time without MIDI output.</summary>
public sealed class NotatedRest
{
    public NoteDuration? StandardDuration { get; init; }
    public double DurationBeats { get; init; } = 1.0;
    public double StartTime { get; set; }
}
