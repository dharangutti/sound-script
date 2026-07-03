namespace SoundScript.Core.Notation;

/// <summary>Validated tie between two notated pitches with identical spelling.</summary>
public sealed class NotatedTie
{
    public NotatedNote Source { get; init; } = null!;
    public NotatedNote Target { get; init; } = null!;
    public double CombinedDurationBeats => Source.DurationBeats + Target.DurationBeats;
}
