using SoundScript.Core.Notation;
using SoundScript.Core.Ast;

namespace SoundScript.Core.Performance;

/// <summary>Score identity is kept separately from jittered playback timing.</summary>
public sealed record PerformanceIntent(double StartBeat, double DurationBeats, int Phrase,
    ArticulationType? Articulation, int Program, int EnvelopeDirection = 0,
    PhraseCurveType? Curve = null, PhraseTransitionMode? Transition = null, bool IsChord = false);

public sealed record PerformanceEvent(int Pitch, double Start, double Duration, double ScoreStart,
    double ScoreDuration, double Velocity, PerformanceIntent Intent);

/// <summary>Physical units, usable by MIDI exporters and continuous audio renderers.</summary>
public sealed record PerformanceShape(double Duration, double Velocity, double Attack, double Release,
    double GainEnd, double Evolution, bool Connected, bool PhraseEnd, double Start);

/// <summary>
/// Pure, renderer-independent interpretation of one monophonic voice. Score rests and phrase IDs
/// are hard boundaries; timing jitter is not allowed to manufacture a connection across them.
/// Chords must be passed as separate voices or excluded by the adapter.
/// </summary>
public static class PerformancePlanner
{
    public const double MaximumOverlapSeconds = 0.04;
    public const int MaximumConnectedInterval = 5;

    public static bool IsSustained(int program) => program is >= 16 and <= 23 or >= 40 and <= 44
        or >= 48 and <= 54 or >= 56 and <= 79 or >= 88 and <= 95;

    public static IReadOnlyList<PerformanceShape> Plan(IReadOnlyList<PerformanceEvent> notes)
    {
        // Clamp jitter to each written event's neighbourhood. Phrase attacks cannot move into a rest.
        notes = notes.Select((n, i) => n with
        {
            Start = Math.Clamp(n.Start, n.ScoreStart - (i > 0 && Adjacent(notes[i - 1], n) ?
                Math.Min(0.03, n.ScoreDuration * 0.1) : 0), n.ScoreStart + n.ScoreDuration * 0.1)
        }).ToArray();
        var starts = new int[notes.Count];
        var ends = new int[notes.Count];
        for (var i = 0; i < notes.Count; i++)
            starts[i] = i > 0 && Adjacent(notes[i - 1], notes[i]) ? starts[i - 1] : i;
        for (var i = notes.Count - 1; i >= 0; i--)
            ends[i] = i + 1 < notes.Count && Adjacent(notes[i], notes[i + 1]) ? ends[i + 1] : i;
        var result = new PerformanceShape[notes.Count];
        for (var i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            var previous = i > 0 ? notes[i - 1] : null;
            var next = i + 1 < notes.Count ? notes[i + 1] : null;
            var connected = previous is not null && Connects(previous, n);
            var connectsNext = next is not null && Connects(n, next);
            var phraseEnd = next is null || !Adjacent(n, next);
            var sustained = IsSustained(n.Intent.Program);
            var staccato = n.Intent.Articulation == ArticulationType.Staccato;
            var accent = n.Intent.Articulation == ArticulationType.Accent;
            var duration = n.Duration;
            var release = staccato ? 0.012 : sustained ? 0.04 : 0.025;
            var attack = staccato || accent ? 0.004 : sustained ? (connected ? 0.035 : 0.018) : 0.006;

            if (connectsNext)
            {
                var overlap = Math.Min(MaximumOverlapSeconds, Math.Min(n.ScoreDuration, next!.ScoreDuration) * 0.08);
                duration = next.Start - n.Start + overlap;
                release = Math.Min(0.02, overlap);
            }
            else if (!staccato)
            {
                // Leave space for a fresh attack on repetitions/leaps and fit the release before a breath.
                var end = Math.Min(n.ScoreStart + n.ScoreDuration, next?.Start ?? double.PositiveInfinity);
                var gap = !phraseEnd ? Math.Min(0.02, n.ScoreDuration * 0.08) : 0;
                release = Math.Min(release, Math.Max(0, end - n.Start - gap) * 0.2);
                duration = Math.Min(duration, end - n.Start - gap - release);
            }
            // Even short staccato release tails may not enter an explicit rest or the next attack.
            if (!connectsNext)
                release = Math.Min(release, Math.Max(0, Math.Min(n.ScoreStart + n.ScoreDuration,
                    next?.Start ?? double.PositiveInfinity) - n.Start - Math.Max(0, duration)));

            var first = starts[i];
            var last = ends[i];
            var position = last == first ? 0.5 : (i - first) / (double)(last - first);
            // Protect explicitly articulated rhythm; other lines arch toward their middle.
            var contour = staccato || accent ? 1 : 0.94 + 0.10 * Math.Sin(Math.PI * position);
            if (phraseEnd && !staccato && !accent && last > first) contour *= 0.96;

            var span = notes[last].ScoreStart + notes[last].ScoreDuration - notes[first].ScoreStart;
            var startPosition = span <= 0 ? 0 : (n.ScoreStart - notes[first].ScoreStart) / span;
            var endPosition = span <= 0 ? 1 : (n.ScoreStart + n.ScoreDuration - notes[first].ScoreStart) / span;
            var direction = n.Intent.EnvelopeDirection;
            double Gain(double p) => 1 + direction * (0.30 * p - 0.15);
            var gainEnd = direction == 0 ? 1 : Gain(endPosition) / Gain(startPosition);
            var velocity = n.Velocity;
            if (n.Intent.Curve is { } curve)
            {
                velocity = curve switch
                {
                    PhraseCurveType.Soft => Math.Sqrt(velocity),
                    PhraseCurveType.Hard => velocity * velocity,
                    PhraseCurveType.Expressive => 0.5 * velocity + 0.5 * Math.Sqrt(velocity),
                    _ => 0.35 * velocity + 0.65 * Math.Sqrt(velocity)
                };
                velocity *= Gain(position);
                velocity *= n.Intent.Transition switch
                {
                    PhraseTransitionMode.Smooth => 0.88 + 0.12 * Math.Sin(Math.PI * position),
                    PhraseTransitionMode.Soft => 0.80 + 0.20 * Math.Sin(Math.PI * position),
                    PhraseTransitionMode.Expressive => 0.90 + 0.10 * Math.Cos(Math.PI * position * 0.5),
                    _ => 1
                };
            }
            result[i] = new PerformanceShape(Math.Max(0.001, duration), Math.Clamp(velocity * contour, 1.0 / 127, 1),
                Math.Min(attack, Math.Max(0.001, duration) * 0.25), release, gainEnd,
                sustained && n.ScoreDuration >= 0.6 && !staccato ? 0.04 : 0, connected, phraseEnd, n.Start);
        }
        return result;
    }

    private static bool Adjacent(PerformanceEvent a, PerformanceEvent b) =>
        a.Intent.Phrase == b.Intent.Phrase && a.Intent.Program == b.Intent.Program &&
        Math.Abs(a.ScoreStart + a.ScoreDuration - b.ScoreStart) < 1e-6;

    private static bool Connects(PerformanceEvent a, PerformanceEvent b) => Adjacent(a, b) &&
        !a.Intent.IsChord && !b.Intent.IsChord &&
        a.Intent.Articulation is not (ArticulationType.Staccato or ArticulationType.Accent) &&
        b.Intent.Articulation is not (ArticulationType.Staccato or ArticulationType.Accent) &&
        (a.Intent.Articulation == ArticulationType.Legato || IsSustained(a.Intent.Program)) &&
        Math.Min(a.ScoreDuration, b.ScoreDuration) >= 0.18 &&
        Math.Abs(a.Pitch - b.Pitch) is > 0 and <= MaximumConnectedInterval && b.Start > a.Start;
}
