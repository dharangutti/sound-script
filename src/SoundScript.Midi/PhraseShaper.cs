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
        velocity = ApplyPositionEnvelope(velocity, scope);
        velocity = ApplyTransitionEnvelope(velocity, scope);

        scope.NoteIndex++;
        return (velocity, velocity != baseVelocity);
    }

    private static int ApplyPositionEnvelope(int velocity, PhraseScope scope)
    {
        if (scope.NoteCount <= 1)
            return velocity;

        var position = scope.NoteIndex / (double)(scope.NoteCount - 1);
        var multiplier = scope.Envelope switch
        {
            PhraseEnvelopeType.Crescendo => 0.85 + 0.30 * position,
            PhraseEnvelopeType.Decrescendo => 1.15 - 0.30 * position,
            _ => scope.Curve switch
            {
                PhraseCurveType.Swell => 0.85 + 0.30 * position,
                PhraseCurveType.Fade => 1.15 - 0.30 * position,
                _ => 1.0
            }
        };

        if (Math.Abs(multiplier - 1.0) < 1e-9)
            return velocity;

        return Math.Clamp((int)Math.Round(velocity * multiplier), 1, 127);
    }

    private static int ApplyTransitionEnvelope(int velocity, PhraseScope scope)
    {
        if (scope.NoteCount <= 1)
            return velocity;

        var position = scope.NoteIndex / (double)(scope.NoteCount - 1);
        var envelope = scope.Transition switch
        {
            PhraseTransitionMode.Smooth => 0.88 + 0.12 * Math.Sin(Math.PI * position),
            PhraseTransitionMode.Soft => 0.80 + 0.20 * Math.Sin(Math.PI * position),
            PhraseTransitionMode.Expressive => 0.90 + 0.10 * Math.Cos(Math.PI * position * 0.5),
            PhraseTransitionMode.Abrupt => 1.0,
            _ => 1.0
        };

        if (Math.Abs(envelope - 1.0) < 1e-9)
            return velocity;

        return Math.Clamp((int)Math.Round(velocity * envelope), 1, 127);
    }

    private static VelocityCurveType ToVelocityCurve(PhraseCurveType curve) => curve switch
    {
        PhraseCurveType.Soft => VelocityCurveType.Soft,
        PhraseCurveType.Hard => VelocityCurveType.Hard,
        PhraseCurveType.Balanced => VelocityCurveType.Balanced,
        PhraseCurveType.Expressive => VelocityCurveType.Expressive,
        PhraseCurveType.Swell => VelocityCurveType.Balanced,
        PhraseCurveType.Fade => VelocityCurveType.Balanced,
        _ => VelocityCurveType.Balanced
    };
}
