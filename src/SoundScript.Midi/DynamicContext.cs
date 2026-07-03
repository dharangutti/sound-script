using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Ramps velocity across abrupt dynamic changes for musical transitions.</summary>
internal sealed class DynamicRampState
{
    public int NotesRemaining { get; set; }
    public int TotalNotes { get; init; }
    public int FromVelocity { get; init; }
    public int ToVelocity { get; init; }
}

internal static class DynamicContext
{
    internal const int RampNotes = 3;
    private const int AbruptThreshold = 24;

    internal static bool IsAbruptChange(DynamicLevel? previous, DynamicLevel current)
    {
        if (previous is null)
            return false;

        return Math.Abs(current.ToVelocity() - previous.Value.ToVelocity()) >= AbruptThreshold;
    }

    internal static DynamicRampState StartRamp(DynamicLevel? previous, DynamicLevel current) =>
        new()
        {
            TotalNotes = RampNotes,
            NotesRemaining = RampNotes,
            FromVelocity = previous?.ToVelocity() ?? DynamicLevel.MezzoPiano.ToVelocity(),
            ToVelocity = current.ToVelocity()
        };

    internal static (int? Velocity, bool Applied) Resolve(DynamicRampState? ramp)
    {
        if (ramp is null || ramp.NotesRemaining <= 0)
            return (null, false);

        var step = ramp.TotalNotes - ramp.NotesRemaining;
        var progress = (step + 1) / (double)ramp.TotalNotes;
        var velocity = (int)Math.Round(ramp.FromVelocity + (ramp.ToVelocity - ramp.FromVelocity) * progress);
        ramp.NotesRemaining--;
        return (Math.Clamp(velocity, 1, 127), true);
    }
}
