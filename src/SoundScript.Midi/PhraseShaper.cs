using SoundScript.Core.Ast;

namespace SoundScript.Midi;

/// <summary>Phrase-level shaping applied before playback shaping.</summary>
internal static class PhraseShaper
{
    internal static (int Velocity, bool Shaped) Apply(int baseVelocity, PhraseScope? scope)
    {
        if (scope is null)
            return (baseVelocity, false);

        var velocity = VelocityCurve.Apply(baseVelocity, ToVelocityCurve(scope.Curve));

        if (scope.Transition == PhraseTransitionMode.Smooth && scope.NoteCount > 1)
        {
            var position = scope.NoteIndex / (double)(scope.NoteCount - 1);
            var envelope = 0.88 + 0.12 * Math.Sin(Math.PI * position);
            velocity = Math.Clamp((int)Math.Round(velocity * envelope), 1, 127);
        }

        scope.NoteIndex++;
        return (velocity, velocity != baseVelocity);
    }

    private static VelocityCurveType ToVelocityCurve(PhraseCurveType curve) => curve switch
    {
        PhraseCurveType.Soft => VelocityCurveType.Soft,
        PhraseCurveType.Hard => VelocityCurveType.Hard,
        PhraseCurveType.Balanced => VelocityCurveType.Balanced,
        _ => VelocityCurveType.Balanced
    };
}
