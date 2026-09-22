using System.Globalization;
using System.Xml.Linq;

namespace SoundScript.Media;

/// <summary>Paints a typed scene as safe, deterministic SVG in the logical 1280 by 720 viewport.</summary>
/// <remarks>Canonical shape paths already include rotation. Legacy named visuals use a simple card/ellipse
/// presentation; decorative raster effects are not reproduced. No source, timing, or interpolation is evaluated.</remarks>
public static class TemporalSvgRenderer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>Renders paths and labels in scene order, escaping all XML text and attributes.</summary>
    /// <exception cref="ArgumentException">A supplied path paint is neither none nor a #RRGGBB color.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied path coordinate or width is not finite.</exception>
    public static string Render(TemporalVisualScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var root = new XElement(Svg + "svg", new XAttribute("viewBox", "0 0 1280 720"),
            new XAttribute("width", "1280"), new XAttribute("height", "720"), new XAttribute("role", "img"));
        foreach (var p in scene.Primitives)
        {
            var group = new XElement(Svg + "g", new XAttribute("data-name", p.Name),
                new XAttribute("opacity", Number(p.Opacity)), new XElement(Svg + "title", p.Label));
            if (p.Paths is { } paths)
            {
                foreach (var path in paths)
                {
                    if (path.Points.Count == 0) continue;
                    var data = "M " + string.Join(" L ", path.Points.Select(point => Number(point.X) + " " + Number(point.Y))) + (path.Closed ? " Z" : "");
                    group.Add(new XElement(Svg + "path", new XAttribute("d", data),
                        new XAttribute("fill", Paint(path.Closed ? path.Fill : "none")),
                        new XAttribute("stroke", Paint(path.Stroke)), new XAttribute("stroke-width", Number(path.StrokeWidth)),
                        new XAttribute("stroke-linecap", "round"), new XAttribute("stroke-linejoin", "round")));
                }
            }
            else
            {
                var cx = p.Left + p.Width / 2;
                var cy = p.Top + p.Height / 2;
                group.Add(new XAttribute("transform", $"rotate({Number(p.RotationDegrees)} {Number(cx)} {Number(cy)})"));
                if (p.Kind == "circle")
                    group.Add(new XElement(Svg + "ellipse", new XAttribute("cx", Number(cx)), new XAttribute("cy", Number(cy)),
                        new XAttribute("rx", Number(p.Width / 2)), new XAttribute("ry", Number(p.Height / 2)), new XAttribute("fill", "#6ee7b7")));
                else if (p.Kind != "sparkle")
                    group.Add(new XElement(Svg + "rect", new XAttribute("x", Number(p.Left)), new XAttribute("y", Number(p.Top)),
                        new XAttribute("width", Number(p.Width)), new XAttribute("height", Number(p.Height)),
                        new XAttribute("rx", "8"), new XAttribute("fill", "#0f182b"), new XAttribute("stroke", "#a5f3fc")));
                group.Add(new XElement(Svg + "text", new XAttribute("x", Number(cx)), new XAttribute("y", Number(cy)),
                    new XAttribute("text-anchor", "middle"), new XAttribute("dominant-baseline", "middle"),
                    new XAttribute("font-family", "sans-serif"), new XAttribute("font-size", "24"), new XAttribute("fill", "#ffffff"), p.Label));
            }
            root.Add(group);
        }
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Number(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "SVG numbers must be finite.");
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
    private static string Paint(string value) => value == "none" ||
        (value.Length == 7 && value[0] == '#' && value.AsSpan(1).ToString().All(Uri.IsHexDigit))
        ? value : throw new ArgumentException("SVG paints must be none or #RRGGBB colors.", nameof(value));
}
