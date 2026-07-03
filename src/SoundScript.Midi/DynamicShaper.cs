using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Applies dynamic-level shaping curves after ramping.</summary>
internal static class DynamicShaper
{
    internal static (int Velocity, bool Shaped) Apply(DynamicLevel? dynamicLevel, int baseVelocity)
    {
        if (dynamicLevel is null)
            return (baseVelocity, false);

        var normalized = Math.Clamp(baseVelocity, 1, 127) / 127.0;
        var curved = dynamicLevel.Value switch
        {
            DynamicLevel.Piano => Math.Pow(normalized, 1.25),
            DynamicLevel.MezzoPiano => Math.Pow(normalized, 1.08),
            DynamicLevel.MezzoForte => Math.Pow(normalized, 0.92),
            DynamicLevel.Forte => Math.Pow(normalized, 0.78),
            _ => normalized
        };

        var shaped = Math.Clamp((int)Math.Round(curved * 127.0, MidpointRounding.AwayFromZero), 1, 127);
        return (shaped, shaped != baseVelocity);
    }
}
