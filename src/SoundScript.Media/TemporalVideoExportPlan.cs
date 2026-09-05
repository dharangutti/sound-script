using SoundScript.Visual;

namespace SoundScript.Media;

/// <summary>
/// Renderer-only sampling settings. FPS is deliberately absent from the
/// SoundScript AST and <see cref="VisualTimeline"/>; it belongs here because a
/// conventional video container needs samples of the temporal program.
/// </summary>
public sealed record TemporalVideoExportSettings(int FramesPerSecond = 30)
{
    public void Validate()
    {
        if (FramesPerSecond is not (24 or 30 or 60))
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond), "Video export supports 24, 30, or 60 FPS.");
    }
}

/// <summary>A renderer-facing, immutable observation of <see cref="VisualTimeline.StateAt"/>.</summary>
public sealed record TemporalVideoExportPlan(
    int FramesPerSecond,
    double DurationSeconds,
    IReadOnlyList<TemporalVideoSample> Samples);

/// <summary>A sampled instant. It is not an authored frame or a timeline track.</summary>
public sealed record TemporalVideoSample(double TimeSeconds, IReadOnlyList<TemporalVideoElement> Elements);

/// <summary>A renderer-friendly visual state that has already been evaluated by the temporal timeline.</summary>
public sealed record TemporalVideoElement(
    string Name,
    IReadOnlyList<TemporalVideoProperty> Properties);

public sealed record TemporalVideoProperty(string Name, decimal Value);

/// <summary>
/// Converts the authoritative temporal function into observations for an
/// output adapter. The exporter never interprets intervals or automation
/// itself: every sample comes directly from <see cref="VisualTimeline.StateAt"/>.
/// </summary>
public static class TemporalVideoExportPlanBuilder
{
    public static TemporalVideoExportPlan Build(VisualTimeline timeline, TemporalVideoExportSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        settings ??= new TemporalVideoExportSettings();
        settings.Validate();

        var frameCount = checked((int)Math.Ceiling(timeline.Duration.TotalSeconds * settings.FramesPerSecond));
        var samples = new TemporalVideoSample[frameCount];
        for (var index = 0; index < frameCount; index++)
        {
            var time = TimeSpan.FromSeconds((double)index / settings.FramesPerSecond);
            var state = timeline.StateAt(time);
            samples[index] = new TemporalVideoSample(
                time.TotalSeconds,
                Array.AsReadOnly(state.Elements.Select(element => new TemporalVideoElement(
                    element.Name,
                    Array.AsReadOnly(element.Properties.Select(property =>
                        new TemporalVideoProperty(property.Property, property.Value)).ToArray()))).ToArray()));
        }

        return new TemporalVideoExportPlan(
            settings.FramesPerSecond,
            timeline.Duration.TotalSeconds,
            Array.AsReadOnly(samples));
    }
}
