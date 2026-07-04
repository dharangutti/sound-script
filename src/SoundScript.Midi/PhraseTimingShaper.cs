using SoundScript.Core.Ast;

namespace SoundScript.Midi;

/// <summary>Deterministic phrase-level timing offsets applied before humanization.</summary>
internal static class PhraseTimingShaper
{
    internal static double Apply(double startBeat, double durationBeats, PhraseScope scope)
    {
        var beat = startBeat;

        if (scope.PullBeats > 0)
            beat += scope.PullBeats;

        if (scope.PushBeats > 0)
            beat -= scope.PushBeats;

        if (scope.SwingRatio is > 0 and var swing && scope.NoteIndex % 2 == 1)
            beat += durationBeats * (1.0 - swing) * 0.5;

        return BeatMath.RoundBeat(Math.Max(0, beat));
    }
}
