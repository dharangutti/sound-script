using SoundScript.Core.Notation;

namespace SoundScript.Midi;

internal readonly record struct ArticulationShapeResult(int Velocity, double DurationBeats, bool Shaped);

/// <summary>Refines articulation-specific velocity and duration shaping.</summary>
internal static class ArticulationShaper
{
    internal static ArticulationShapeResult Apply(
        ArticulationType? articulation,
        int velocity,
        double durationBeats)
    {
        if (articulation is null)
            return new ArticulationShapeResult(velocity, durationBeats, false);

        return articulation switch
        {
            ArticulationType.Staccato => new(
                Math.Max(1, (int)Math.Round(velocity * 0.92)),
                BeatMath.RoundBeat(durationBeats * 0.47),
                true),
            ArticulationType.Legato => new(
                velocity,
                BeatMath.RoundBeat(durationBeats * 0.97),
                true),
            ArticulationType.Accent => new(
                Math.Min(127, (int)Math.Round(velocity * 1.1)),
                BeatMath.RoundBeat(durationBeats * 1.02),
                true),
            _ => new ArticulationShapeResult(velocity, durationBeats, false)
        };
    }
}
