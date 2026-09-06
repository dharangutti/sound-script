using SoundScript.Core;
using SoundScript.Core.Ast;

namespace SoundScript.Visual;

/// <summary>
/// An immutable temporal visual program. Querying it at any time is pure and
/// deterministic; export layers may sample it at any FPS without changing this
/// source-level model.
/// </summary>
public sealed class VisualTimeline
{
    private readonly IReadOnlyList<ScheduledVisual> _visuals;

    internal VisualTimeline(
        IReadOnlyList<ScheduledVisual> visuals,
        IReadOnlyList<AudioSyncPoint> audioSyncPoints,
        TimeSpan duration)
    {
        _visuals = visuals;
        AudioSyncPoints = audioSyncPoints;
        Duration = duration;
    }

    /// <summary>All scheduled visual intervals, ordered by start then source order.</summary>
    public IReadOnlyList<ScheduledVisual> Visuals => _visuals;

    /// <summary>Declared points where the visual narrative shares the audio clock.</summary>
    public IReadOnlyList<AudioSyncPoint> AudioSyncPoints { get; }

    /// <summary>The greater of the narrative cursor and every scheduled interval end.</summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Evaluates the complete visual state at an arbitrary absolute time.
    /// Intervals are half-open: a visual ending at t=4s is absent at exactly t=4s.
    /// </summary>
    public VisualState StateAt(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(time), "Visual state cannot be queried before t=0.");

        var elements = new List<VisualElementState>();
        foreach (var visual in _visuals)
        {
            if (time < visual.Start || time >= visual.End)
                continue;

            var localTime = time - visual.Start;
            var properties = Array.AsReadOnly(visual.Automations
                .Select(automation => new VisualPropertyValue(
                    automation.Property,
                    automation.ValueAt(localTime)))
                .ToArray());

            elements.Add(new VisualElementState(
                visual.Name,
                visual.Start,
                visual.End,
                properties, visual.Presentation));
        }

        return new VisualState(time, Array.AsReadOnly(elements.ToArray()));
    }

    /// <summary>
    /// Converts a SoundScript score beat to the shared elapsed-time clock using
    /// the authoritative tempo map, then evaluates the visual state there.
    /// This is a bridge for audio synchronization; it does not make beat or FPS
    /// a requirement of the visual source language.
    /// </summary>
    public VisualState StateAtAudioBeat(double beat, TempoAutomationMap tempoMap)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        if (!double.IsFinite(beat) || beat < 0)
            throw new ArgumentOutOfRangeException(nameof(beat), "Audio beat must be a finite, non-negative value.");

        var milliseconds = tempoMap.BeatsToMilliseconds(0, beat);
        return StateAt(FromMilliseconds(milliseconds));
    }

    private static TimeSpan FromMilliseconds(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
            throw new InvalidOperationException("Tempo map produced an invalid elapsed time.");

        try
        {
            var ticks = decimal.ToInt64(decimal.Round(
                (decimal)milliseconds * TimeSpan.TicksPerMillisecond,
                0,
                MidpointRounding.AwayFromZero));
            return TimeSpan.FromTicks(ticks);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException("Tempo map elapsed time is outside the supported visual time range.");
        }
    }
}

/// <summary>A scheduled visual interval in the compiled temporal program.</summary>
public sealed record ScheduledVisual(
    string Name,
    TimeSpan Start,
    TimeSpan End,
    int SourceOrder,
    IReadOnlyList<ScheduledVisualAutomation> Automations,
    VisualPresentation? Presentation = null)
{
    public TimeSpan Duration => End - Start;
}

/// <summary>A deterministic, local-to-visual property curve.</summary>
public sealed record ScheduledVisualAutomation(
    string Property,
    decimal From,
    decimal To,
    TimeSpan Duration)
{
    public decimal ValueAt(TimeSpan localTime)
    {
        if (localTime <= TimeSpan.Zero)
            return From;

        if (localTime >= Duration)
            return To;

        return From + (To - From) * localTime.Ticks / Duration.Ticks;
    }
}

/// <summary>A marker connecting the visual narrative cursor to the audio clock.</summary>
public sealed record AudioSyncPoint(TimeSpan Time);

/// <summary>The immutable answer to a <see cref="VisualTimeline.StateAt"/> query.</summary>
public sealed record VisualState(TimeSpan Time, IReadOnlyList<VisualElementState> Elements);

/// <summary>An active visual plus its evaluated renderer-facing properties.</summary>
public sealed record VisualElementState(
    string Name,
    TimeSpan Start,
    TimeSpan End,
    IReadOnlyList<VisualPropertyValue> Properties,
    VisualPresentation? Presentation = null);

/// <summary>A sampled automation value at a particular instant.</summary>
public sealed record VisualPropertyValue(string Property, decimal Value);
