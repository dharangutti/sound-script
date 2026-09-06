namespace SoundScript.Core.Ast;

/// <summary>Optional, immutable presentation settings; absent settings preserve legacy named visuals.</summary>
public sealed record VisualPresentation
{
    public string? Shape { get; init; }
    public string? Fill { get; init; }
    public string? Stroke { get; init; }
    public decimal StrokeWidth { get; init; } = 4m;
    public string? Text { get; init; }
    public decimal FontSize { get; init; } = 42m;

    public static bool IsShape(string shape) => shape is
        "rectangle" or "roundedRectangle" or "ellipse" or "circle" or "triangle" or "line" or "arrow" or "ring" or "text";

    public static string NormalizeShape(string shape) => shape.ToLowerInvariant() switch
    {
        "roundedrectangle" => "roundedRectangle",
        var value => value
    };

    public void Validate()
    {
        if (Shape is null || !IsShape(Shape))
            throw new ArgumentException("Presentation settings require shape: rectangle, roundedRectangle, ellipse, circle, triangle, line, arrow, ring, or text.");
        foreach (var color in new[] { Fill, Stroke })
            if (color is not null && color != "none" &&
                (color.Length != 7 || color[0] != '#' || !color.AsSpan(1).ToString().All(Uri.IsHexDigit)))
                throw new ArgumentException("Colors must be quoted #RRGGBB values or \"none\".");
        if (StrokeWidth < 0m || StrokeWidth > 128m)
            throw new ArgumentException("strokeWidth must be between 0 and 128 logical pixels.");
        if (FontSize < 8m || FontSize > 256m)
            throw new ArgumentException("fontSize must be between 8 and 256 logical pixels.");
        if (Text is not null && Shape != "text")
            throw new ArgumentException("text content requires shape text; use a separate text visual for a label.");
        if (Shape == "text" && Text is null)
            throw new ArgumentException("shape text requires a quoted text value.");
        if (Shape is "line" or "ring" && Fill is not null and not "none")
            throw new ArgumentException("Line and ring use stroke, not fill.");
        if (Shape == "text" && Stroke is not null and not "none")
            throw new ArgumentException("Text uses fill; text outlines are not supported.");
        if (Text is not null && (Text.Length > 120 || Text.Any(c =>
                !char.IsAsciiLetterOrDigit(c) && !" .,:;!?+-/%()[]=<>_".Contains(c))))
            throw new ArgumentException("Text supports up to 120 Latin letters, digits, spaces, and .,:;!?+-/%()[]=<>_ punctuation. Lowercase is displayed as uppercase.");
    }
}
