namespace SoundScript.Core;

/// <summary>Global tempo automation with linear ramps and beat-accurate duration integration.</summary>
public sealed class TempoAutomationMap
{
    private readonly List<TempoSegment> _segments = [new ConstantTempoSegment(0, 120)];

    public int InitialBpm { get; private set; } = 120;

    public IReadOnlyList<TempoSegment> Segments => _segments;

    public void SetTempo(double beat, int bpm)
    {
        beat = RoundBeat(Math.Max(0, beat));
        RemoveSegmentsStartingAtOrAfter(beat);
        _segments.Add(new ConstantTempoSegment(beat, bpm));
    }

    public void AddRamp(double startBeat, double durationBeats, int startBpm, int endBpm)
    {
        startBeat = RoundBeat(Math.Max(0, startBeat));
        var endBeat = RoundBeat(startBeat + durationBeats);
        if (endBeat <= startBeat)
            throw new InvalidOperationException("Tempo ramp duration must be greater than zero.");

        RemoveSegmentsStartingAtOrAfter(startBeat);
        _segments.Add(new RampTempoSegment(startBeat, endBeat, startBpm, endBpm));
        _segments.Add(new ConstantTempoSegment(endBeat, endBpm));
    }

    public double GetBpmAt(double beat)
    {
        beat = Math.Max(0, beat);
        TempoSegment? active = null;

        foreach (var segment in _segments.OrderBy(s => s.StartBeat))
        {
            if (segment.StartBeat <= beat + Epsilon)
                active = segment;
            else
                break;
        }

        return active switch
        {
            null => InitialBpm,
            ConstantTempoSegment constant => constant.Bpm,
            RampTempoSegment ramp when beat <= ramp.EndBeat + Epsilon => InterpolateBpm(ramp, beat),
            RampTempoSegment ramp => ramp.EndBpm,
            _ => InitialBpm
        };
    }

    public double BeatsToMilliseconds(double startBeat, double durationBeats)
    {
        if (durationBeats <= 0)
            return 0;

        var endBeat = startBeat + durationBeats;
        var milliseconds = 0.0;
        var position = startBeat;

        foreach (var segment in _segments.OrderBy(s => s.StartBeat))
        {
            if (segment.StartBeat >= endBeat - Epsilon)
                break;

            var segmentEnd = segment switch
            {
                ConstantTempoSegment => NextSegmentStart(segment) ?? double.PositiveInfinity,
                RampTempoSegment ramp => ramp.EndBeat,
                _ => double.PositiveInfinity
            };

            var from = Math.Max(position, segment.StartBeat);
            var to = Math.Min(endBeat, segmentEnd);
            if (to <= from + Epsilon)
                continue;

            milliseconds += segment switch
            {
                ConstantTempoSegment constant => IntegrateConstant(constant.Bpm, to - from),
                RampTempoSegment ramp => IntegrateRamp(ramp, from, to),
                _ => 0
            };

            position = to;
            if (position >= endBeat - Epsilon)
                break;
        }

        if (position < endBeat - Epsilon)
        {
            var tailBpm = GetBpmAt(position);
            milliseconds += IntegrateConstant(tailBpm, endBeat - position);
        }

        return milliseconds;
    }

    public IEnumerable<TempoMapPoint> GetTempoMapPoints()
    {
        foreach (var segment in _segments.OrderBy(s => s.StartBeat))
        {
            switch (segment)
            {
                case ConstantTempoSegment constant:
                    yield return new TempoMapPoint(constant.StartBeat, constant.Bpm);
                    break;
                case RampTempoSegment ramp:
                    yield return new TempoMapPoint(ramp.StartBeat, ramp.StartBpm);
                    var beat = Math.Ceiling(ramp.StartBeat);
                    while (beat < ramp.EndBeat - Epsilon)
                    {
                        yield return new TempoMapPoint(beat, (int)Math.Round(InterpolateBpm(ramp, beat)));
                        beat += 1.0;
                    }
                    yield return new TempoMapPoint(ramp.EndBeat, ramp.EndBpm);
                    break;
            }
        }
    }

    private void RemoveSegmentsStartingAtOrAfter(double beat)
    {
        _segments.RemoveAll(segment => segment.StartBeat >= beat - Epsilon);

        for (var i = _segments.Count - 1; i >= 0; i--)
        {
            if (_segments[i] is not RampTempoSegment ramp || ramp.StartBeat >= beat - Epsilon || ramp.EndBeat <= beat + Epsilon)
                continue;

            var midBpm = (int)Math.Round(InterpolateBpm(ramp, beat));
            _segments[i] = new RampTempoSegment(ramp.StartBeat, beat, ramp.StartBpm, midBpm);
            _segments.Add(new ConstantTempoSegment(beat, midBpm));
            break;
        }
    }

    private double? NextSegmentStart(TempoSegment current)
    {
        var found = false;
        foreach (var segment in _segments.OrderBy(s => s.StartBeat))
        {
            if (segment == current)
            {
                found = true;
                continue;
            }

            if (found)
                return segment.StartBeat;
        }

        return null;
    }

    private static double InterpolateBpm(RampTempoSegment ramp, double beat)
    {
        var progress = (beat - ramp.StartBeat) / (ramp.EndBeat - ramp.StartBeat);
        return ramp.StartBpm + (ramp.EndBpm - ramp.StartBpm) * progress;
    }

    private static double IntegrateConstant(double bpm, double beats) =>
        beats * 60_000.0 / bpm;

    private static double IntegrateRamp(RampTempoSegment ramp, double from, double to)
    {
        var slope = (ramp.EndBpm - ramp.StartBpm) / (ramp.EndBeat - ramp.StartBeat);
        if (Math.Abs(slope) < Epsilon)
            return IntegrateConstant(ramp.StartBpm, to - from);

        var uFrom = ramp.StartBpm + slope * (from - ramp.StartBeat);
        var uTo = ramp.StartBpm + slope * (to - ramp.StartBeat);
        return 60_000.0 / slope * Math.Log(uTo / uFrom);
    }

    private static double RoundBeat(double beats) =>
        Math.Round(beats, 9, MidpointRounding.AwayFromZero);

    private const double Epsilon = 1e-9;
}

public abstract record TempoSegment(double StartBeat);

public sealed record ConstantTempoSegment(double StartBeat, int Bpm) : TempoSegment(StartBeat);

public sealed record RampTempoSegment(double StartBeat, double EndBeat, int StartBpm, int EndBpm) : TempoSegment(StartBeat);

public readonly record struct TempoMapPoint(double Beat, int Bpm);
