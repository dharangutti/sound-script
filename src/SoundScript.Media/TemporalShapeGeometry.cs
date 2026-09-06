using SoundScript.Core.Ast;

namespace SoundScript.Media;

public sealed record TemporalPoint(double X, double Y);
public sealed record TemporalShapePath(
    IReadOnlyList<TemporalPoint> Points, bool Closed, string Fill, string Stroke, double StrokeWidth);

/// <summary>Shared geometry for opt-in primitives. Renderers paint these paths without reinterpreting shapes.</summary>
public static class TemporalShapeGeometry
{
    public static TemporalVisualPrimitive Project(TemporalVideoElement element)
    {
        var style = element.Presentation!;
        style.Validate();
        decimal? Property(string name) => element.Properties.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        var kind = style.Shape!;
        var round = kind is "circle" or "ring";
        var diameter = Math.Clamp(Property("radius") ?? 72m, 4m, 360m) * 2m;
        var width = Math.Clamp(Property("width") ?? Property("size") ?? (round ? diameter : kind == "text" ? 480m : 240m), 8m, 1280m);
        var height = Math.Clamp(Property("height") ?? Property("size") ?? (round ? diameter : kind == "text" ? 80m : 120m), 8m, 720m);
        // Explicit circle/ring remain circular even if their bounding box is rectangular.
        if (round) width = height = Math.Min(width, height);
        var cx = Coordinate(Property("x") ?? .5m, 1280m);
        var cy = Coordinate(Property("y") ?? .5m, 720m);
        var rotation = Math.Clamp(Property("rotation") ?? 0m, -360m, 360m);
        var opacity = Math.Clamp(Property("opacity") ?? 1m, 0m, 1m);
        var primitive = new TemporalVisualPrimitive(element.Name, kind, style.Text ?? "",
            cx - width / 2, cy - height / 2, width, height, opacity, rotation);
        return primitive with { Paths = BuildPaths(primitive, style) };
    }

    private static decimal Coordinate(decimal value, decimal extent) => value is >= 0m and <= 1m ? value * extent : value;

    private static IReadOnlyList<TemporalShapePath> BuildPaths(TemporalVisualPrimitive p, VisualPresentation style)
    {
        var paths = new List<TemporalShapePath>();
        var w = (double)p.Width;
        var h = (double)p.Height;
        var outlineOnly = p.Kind is "line" or "ring";
        var fill = outlineOnly ? "none" : style.Fill ?? "#6ee7b7";
        var stroke = style.Stroke ?? (outlineOnly ? "#6ee7b7" : "none");
        var radians = (double)p.RotationDegrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        TemporalPoint Transform(TemporalPoint point)
        {
            var dx = point.X - w / 2;
            var dy = point.Y - h / 2;
            return new TemporalPoint((double)p.Left + w / 2 + dx * cos - dy * sin,
                (double)p.Top + h / 2 + dx * sin + dy * cos);
        }
        void Add(IEnumerable<TemporalPoint> points, bool closed = true, string? pathFill = null, string? pathStroke = null) =>
            paths.Add(new TemporalShapePath(Array.AsReadOnly(points.Select(Transform).ToArray()), closed,
                pathFill ?? fill, pathStroke ?? stroke, (double)style.StrokeWidth));
        TemporalPoint Pt(double x, double y) => new(x, y);

        switch (p.Kind)
        {
            case "rectangle":
                Add([Pt(0, 0), Pt(w, 0), Pt(w, h), Pt(0, h)]);
                break;
            case "roundedRectangle":
                var radius = Math.Min(14d, Math.Min(w, h) / 2);
                var corners = new[] { Pt(w - radius, radius), Pt(w - radius, h - radius), Pt(radius, h - radius), Pt(radius, radius) };
                Add(corners.SelectMany((center, corner) => Enumerable.Range(0, 9).Select(i =>
                {
                    var angle = (-90 + corner * 90 + i * 90d / 8) * Math.PI / 180;
                    return Pt(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
                })));
                break;
            case "ellipse":
            case "circle":
            case "ring":
                Add(Enumerable.Range(0, 96).Select(i => Pt(w / 2 + w / 2 * Math.Cos(i * Math.Tau / 96),
                    h / 2 + h / 2 * Math.Sin(i * Math.Tau / 96))));
                break;
            case "triangle":
                Add([Pt(w / 2, 0), Pt(w, h), Pt(0, h)]);
                break;
            case "line":
                Add([Pt(0, h / 2), Pt(w, h / 2)], closed: false);
                break;
            case "arrow":
                Add([Pt(0, h * .3), Pt(w * .6, h * .3), Pt(w * .6, 0), Pt(w, h / 2),
                    Pt(w * .6, h), Pt(w * .6, h * .7), Pt(0, h * .7)]);
                break;
            case "text":
                var text = (style.Text ?? "").ToUpperInvariant();
                if (text.Length == 0) break;
                var scale = Math.Min((double)style.FontSize / 7, Math.Min(h / 7, w / (text.Length * 6 - 1)));
                var left = (w - (text.Length * 6 - 1) * scale) / 2;
                var top = (h - 7 * scale) / 2;
                for (var index = 0; index < text.Length; index++)
                {
                    var glyph = MediaGlyphFont.Glyph(text[index]);
                    for (var row = 0; row < 7; row++)
                    for (var col = 0; col < 5; col++)
                    {
                        if (glyph[row][col] != '1') continue;
                        var x = left + (index * 6 + col) * scale;
                        var y = top + row * scale;
                        Add([Pt(x, y), Pt(x + scale, y), Pt(x + scale, y + scale), Pt(x, y + scale)], pathStroke: "none");
                    }
                }
                break;
        }
        return Array.AsReadOnly(paths.ToArray());
    }
}
