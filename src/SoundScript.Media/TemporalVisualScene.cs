using SoundScript.Visual;

namespace SoundScript.Media;

/// <summary>
/// A renderer-facing projection of an already evaluated visual state. Values
/// use the logical 1280 by 720 presentation viewport so browser, CLI, and the
/// Playground stage can scale the same layout without authoring dimensions in
/// the SoundScript language.
/// </summary>
public sealed record TemporalVisualScene(
    double TimeSeconds,
    IReadOnlyList<TemporalVisualPrimitive> Primitives);

/// <summary>
/// A single ordered primitive in the canonical Playground presentation
/// profile. <see cref="Left"/>, <see cref="Top"/>, <see cref="Width"/>, and
/// <see cref="Height"/> are logical pixels in a 1280 by 720 viewport.
/// </summary>
public sealed record TemporalVisualPrimitive(
    string Name,
    string Kind,
    string Label,
    decimal Left,
    decimal Top,
    decimal Width,
    decimal Height,
    decimal Opacity,
    decimal RotationDegrees)
{
    public IReadOnlyList<TemporalShapePath>? Paths { get; init; }
}

/// <summary>
/// The visual export adapter's immutable scene observations. FPS exists on
/// this export object only; the individual scenes are pure projections of
/// <see cref="VisualTimeline.StateAt(TimeSpan)"/> observations.
/// </summary>
public sealed record TemporalVisualExportPlan(
    int FramesPerSecond,
    double DurationSeconds,
    IReadOnlyList<TemporalVisualScene> Samples);

/// <summary>
/// Projects renderer-neutral visual elements into the one presentation profile
/// shared by the Playground preview and both WebM exporters. It deliberately
/// accepts evaluated state/samples rather than a timeline, so it cannot
/// reinterpret intervals, automation, or frame cadence.
/// </summary>
public static class TemporalVisualSceneBuilder
{
    public const decimal LogicalWidth = 1280m;
    public const decimal LogicalHeight = 720m;

    public static TemporalVisualScene Build(VisualState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var sample = new TemporalVideoSample(
            state.Time.TotalSeconds,
            Array.AsReadOnly(state.Elements.Select(element => new TemporalVideoElement(
                element.Name,
                Array.AsReadOnly(element.Properties.Select(property =>
                    new TemporalVideoProperty(property.Property, property.Value)).ToArray()), element.Presentation)).ToArray()));
        return Build(sample);
    }

    public static TemporalVisualScene Build(TemporalVideoSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var primitives = sample.Elements
            .Select(Project)
            .ToArray();
        return new TemporalVisualScene(sample.TimeSeconds, Array.AsReadOnly(primitives));
    }

    public static TemporalVisualExportPlan Build(TemporalVideoExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new TemporalVisualExportPlan(
            plan.FramesPerSecond,
            plan.DurationSeconds,
            Array.AsReadOnly(plan.Samples.Select(Build).ToArray()));
    }

    private static TemporalVisualPrimitive Project(TemporalVideoElement element)
    {
        if (element.Presentation is not null)
            return TemporalShapeGeometry.Project(element);
        var kind = KnownKind(element.Name);
        var (label, left, top, width, height, rotation) = kind switch
        {
            "intro" => ("A visual idea begins", 102.4m, 93.6m, 368m, 70m, 0m),
            "circle" => CircleBounds(element),
            "product" => ("PRODUCT", 115.2m, 474.4m, 288m, 152m, -3m),
            "sparkle" => ("✦", 1041.6m, 57.6m, 72m, 72m, 0m),
            _ => (element.Name, 1152m, 640m, 112m, 48m, 0m),
        };

        // x and y retain the established renderer convention: values from
        // zero to one describe a normalized centre point; larger values are
        // logical pixels in the shared presentation viewport. This gives
        // custom scripts a portable placement escape hatch without putting
        // output dimensions into the temporal DSL.
        if (Property(element, "width") is { } authoredWidth)
            width = ClampDimension(authoredWidth, LogicalWidth);
        else if (Property(element, "size") is { } size)
            width = ClampDimension(size, LogicalWidth);

        if (Property(element, "height") is { } authoredHeight)
            height = ClampDimension(authoredHeight, LogicalHeight);
        else if (Property(element, "size") is { } size)
            height = ClampDimension(size, LogicalHeight);

        if (Property(element, "x") is { } x)
            left = Coordinate(x, LogicalWidth) - width / 2m;
        if (Property(element, "y") is { } y)
            top = Coordinate(y, LogicalHeight) - height / 2m;
        if (Property(element, "rotation") is { } authoredRotation)
            rotation = Math.Clamp(authoredRotation, -360m, 360m);

        return new TemporalVisualPrimitive(
            element.Name,
            kind,
            label,
            left,
            top,
            width,
            height,
            Math.Clamp(Property(element, "opacity") ?? 1m, 0m, 1m),
            rotation);
    }

    private static (string Label, decimal Left, decimal Top, decimal Width, decimal Height, decimal Rotation) CircleBounds(TemporalVideoElement element)
    {
        var radius = Math.Clamp(Property(element, "radius") ?? Property(element, "size") ?? 72m, 12m, 220m);
        var diameter = radius * 2m;
        return ("●", LogicalWidth - 102.4m - diameter, LogicalHeight - 64.8m - diameter, diameter, diameter, 0m);
    }

    private static string KnownKind(string name) => name.Trim().ToLowerInvariant() switch
    {
        "intro" => "intro",
        "circle" => "circle",
        "product" => "product",
        "sparkle" => "sparkle",
        _ => "generic",
    };

    private static decimal? Property(TemporalVideoElement element, string name) =>
        element.Properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static decimal Coordinate(decimal value, decimal logicalSize) =>
        value is >= 0m and <= 1m ? value * logicalSize : value;

    private static decimal ClampDimension(decimal value, decimal maximum) =>
        Math.Clamp(value, 8m, maximum);
}
