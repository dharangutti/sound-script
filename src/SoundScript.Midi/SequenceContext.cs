using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Playback context inherited when a sequence is played from a parent track.</summary>
internal sealed class SequenceContext
{
    public int ProgramNumber { get; init; }
    public string? InstrumentName { get; init; }
    public int Velocity { get; init; }
    public DynamicLevel? Dynamic { get; init; }
}
