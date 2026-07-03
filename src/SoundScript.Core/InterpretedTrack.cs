namespace SoundScript.Core;

public sealed class InterpretedTrack
{
    public string Name { get; init; } = "default";
    public List<TimedNote> Notes { get; } = [];
    public List<ProgramChange> ProgramChanges { get; } = [];
}

public readonly record struct ProgramChange(double Beat, int ProgramNumber, byte Channel = 0);
