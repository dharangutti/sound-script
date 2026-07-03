namespace SoundScript.Midi;

/// <summary>Internal velocity shaping curves for expressive playback.</summary>
internal enum VelocityCurveType
{
    Linear,
    Soft,
    Hard,
    Balanced
}

internal static class VelocityCurve
{
    internal static int Apply(int velocity, VelocityCurveType curve = VelocityCurveType.Linear)
    {
        var normalized = Math.Clamp(velocity, 1, 127) / 127.0;
        var curved = curve switch
        {
            VelocityCurveType.Soft => Math.Sqrt(normalized),
            VelocityCurveType.Hard => normalized * normalized,
            VelocityCurveType.Balanced => 0.35 * normalized + 0.65 * Math.Sqrt(normalized),
            _ => normalized
        };

        return Math.Clamp((int)Math.Round(curved * 127.0, MidpointRounding.AwayFromZero), 1, 127);
    }

    internal static VelocityCurveType ForArticulation(SoundScript.Core.Notation.ArticulationType? articulation) =>
        articulation switch
        {
            SoundScript.Core.Notation.ArticulationType.Legato => VelocityCurveType.Soft,
            SoundScript.Core.Notation.ArticulationType.Accent => VelocityCurveType.Hard,
            _ => VelocityCurveType.Linear
        };
}
