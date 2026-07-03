using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Expressive velocity curves for natural phrasing.</summary>
internal static class ExpressiveCurve
{
    internal static (int Velocity, bool Applied) Apply(int velocity, ArticulationType? articulation)
    {
        var curved = articulation switch
        {
            ArticulationType.Legato => ApplySoft(velocity),
            ArticulationType.Accent => ApplyHard(velocity),
            _ => ApplyBalanced(velocity)
        };

        return (curved, curved != velocity);
    }

    private static int ApplySoft(int velocity)
    {
        var normalized = Math.Clamp(velocity, 1, 127) / 127.0;
        return Math.Clamp((int)Math.Round(Math.Sqrt(normalized) * 127.0), 1, 127);
    }

    private static int ApplyHard(int velocity)
    {
        var normalized = Math.Clamp(velocity, 1, 127) / 127.0;
        return Math.Clamp((int)Math.Round(normalized * normalized * 127.0), 1, 127);
    }

    private static int ApplyBalanced(int velocity)
    {
        var normalized = Math.Clamp(velocity, 1, 127) / 127.0;
        var linear = normalized;
        var soft = Math.Sqrt(normalized);
        var blended = 0.35 * linear + 0.65 * soft;
        return Math.Clamp((int)Math.Round(blended * 127.0), 1, 127);
    }
}
