using System.Text;

namespace SoundScript.Media;

/// <summary>
/// Rasterizes the canonical presentation scene for the FFmpeg adapter. It
/// never reads a timeline: callers supply state observations that were already
/// obtained from <c>VisualTimeline.StateAt(t)</c>.
/// </summary>
public static class TemporalVideoFrameRenderer
{
    public static void WritePpmFrames(
        TemporalVideoExportPlan plan,
        string outputDirectory,
        int width = 640,
        int height = 360) =>
        WritePpmFrames(TemporalVisualSceneBuilder.Build(plan), outputDirectory, width, height);

    public static void WritePpmFrames(
        TemporalVisualExportPlan plan,
        string outputDirectory,
        int width = 640,
        int height = 360)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ValidateOutput(outputDirectory, width, height);

        Directory.CreateDirectory(outputDirectory);
        for (var index = 0; index < plan.Samples.Count; index++)
        {
            var bytes = RenderPpm(plan.Samples[index], width, height);
            File.WriteAllBytes(Path.Combine(outputDirectory, $"frame-{index:D6}.ppm"), bytes);
        }
    }

    /// <summary>Projects a supplied state observation through the shared scene profile.</summary>
    public static byte[] RenderPpm(TemporalVideoSample sample, int width, int height) =>
        RenderPpm(TemporalVisualSceneBuilder.Build(sample), width, height);

    /// <summary>Renders a canonical scene to portable pixmap bytes for FFmpeg image2 input.</summary>
    public static byte[] RenderPpm(TemporalVisualScene scene, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateDimensions(width, height);

        var header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        var pixels = new byte[checked(width * height * 3)];
        PaintBackground(pixels, width, height);

        foreach (var primitive in scene.Primitives)
            PaintPrimitive(pixels, width, height, primitive);

        var result = new byte[header.Length + pixels.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(pixels, 0, result, header.Length, pixels.Length);
        return result;
    }

    private static void ValidateOutput(string outputDirectory, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        ValidateDimensions(width, height);
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width is < 2 or > 3840 || height is < 2 or > 2160 || width % 2 != 0 || height % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Video frame dimensions must be even and within 2–3840 × 2–2160.");
    }

    private static void PaintBackground(byte[] pixels, int width, int height)
    {
        var topLeft = new Rgb(16, 25, 42);
        var bottomRight = new Rgb(14, 28, 38);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var diagonal = ((double)x / Math.Max(1, width - 1) + (double)y / Math.Max(1, height - 1)) / 2d;
            SetPixel(pixels, width, x, y, Lerp(topLeft, bottomRight, diagonal));
        }

        PaintRadialGlow(pixels, width, height, width * .75, height * .15, Math.Min(width, height) * .30, new Rgb(110, 231, 183), .18);
        PaintRadialGlow(pixels, width, height, width * .18, height * .90, Math.Min(width, height) * .38, new Rgb(129, 140, 248), .25);

        var gridX = Math.Max(8, (int)Math.Round(width * 32d / (double)TemporalVisualSceneBuilder.LogicalWidth));
        var gridY = Math.Max(8, (int)Math.Round(height * 32d / (double)TemporalVisualSceneBuilder.LogicalHeight));
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (x % gridX == 0 || y % gridY == 0)
                BlendPixel(pixels, width, x, y, new Rgb(255, 255, 255), .07);
        }
    }

    private static void PaintRadialGlow(byte[] pixels, int width, int height, double centerX, double centerY, double radius, Rgb color, double opacity)
    {
        var minX = Math.Max(0, (int)Math.Floor(centerX - radius));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX + radius));
        var minY = Math.Max(0, (int)Math.Floor(centerY - radius));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY + radius));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
            if (distance < radius)
                BlendPixel(pixels, width, x, y, color, opacity * (1d - distance / radius));
        }
    }

    private static void PaintPrimitive(byte[] pixels, int width, int height, TemporalVisualPrimitive primitive)
    {
        var opacity = (double)Math.Clamp(primitive.Opacity, 0m, 1m);
        if (opacity <= 0)
            return;

        var scaleX = width / (double)TemporalVisualSceneBuilder.LogicalWidth;
        var scaleY = height / (double)TemporalVisualSceneBuilder.LogicalHeight;
        var left = (double)primitive.Left * scaleX;
        var top = (double)primitive.Top * scaleY;
        var elementWidth = Math.Max(1d, (double)primitive.Width * scaleX);
        var elementHeight = Math.Max(1d, (double)primitive.Height * scaleY);

        switch (primitive.Kind)
        {
            case "intro":
                PaintRoundedCard(pixels, width, height, left, top, elementWidth, elementHeight,
                    new Rgb(15, 24, 43), new Rgb(255, 255, 255), opacity * .72, .40, elementHeight / 2);
                DrawText(pixels, width, height, primitive.Label, left + elementWidth * .07, top + elementHeight * .31,
                    elementWidth * .86, elementHeight * .38, new Rgb(238, 246, 255), opacity);
                break;
            case "circle":
                PaintOrb(pixels, width, height, left, top, elementWidth, elementHeight, opacity);
                break;
            case "product":
                PaintProductCard(pixels, width, height, left, top, elementWidth, elementHeight,
                    (double)primitive.RotationDegrees, opacity);
                DrawText(pixels, width, height, primitive.Label, left + elementWidth * .15, top + elementHeight * .42,
                    elementWidth * .70, elementHeight * .18, new Rgb(7, 27, 23), opacity);
                break;
            case "sparkle":
                PaintSparkle(pixels, width, height, left, top, elementWidth, elementHeight, opacity);
                break;
            default:
                PaintRoundedCard(pixels, width, height, left, top, elementWidth, elementHeight,
                    new Rgb(15, 19, 26), new Rgb(110, 231, 183), opacity * .80, 1, Math.Min(8 * scaleX, elementHeight / 3));
                DrawText(pixels, width, height, primitive.Label, left + elementWidth * .10, top + elementHeight * .34,
                    elementWidth * .80, elementHeight * .32, new Rgb(232, 236, 244), opacity);
                break;
        }
    }

    private static void PaintRoundedCard(byte[] pixels, int width, int height, double left, double top, double cardWidth, double cardHeight, Rgb fill, Rgb border, double fillOpacity, double borderOpacity, double radius)
    {
        FillRoundedRect(pixels, width, height, left, top, cardWidth, cardHeight, radius, border, borderOpacity);
        FillRoundedRect(pixels, width, height, left + 1, top + 1, Math.Max(0, cardWidth - 2), Math.Max(0, cardHeight - 2), Math.Max(0, radius - 1), fill, fillOpacity);
    }

    private static void PaintOrb(byte[] pixels, int width, int height, double left, double top, double orbWidth, double orbHeight, double opacity)
    {
        var centerX = left + orbWidth / 2;
        var centerY = top + orbHeight / 2;
        var radiusX = orbWidth / 2;
        var radiusY = orbHeight / 2;
        var minX = Math.Max(0, (int)Math.Floor(left - 5));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(left + orbWidth + 5));
        var minY = Math.Max(0, (int)Math.Floor(top - 5));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(top + orbHeight + 5));

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = (x - centerX) / radiusX;
            var dy = (y - centerY) / radiusY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 1.08)
                continue;

            if (distance > 1)
            {
                BlendPixel(pixels, width, x, y, new Rgb(110, 231, 183), opacity * .09 * (1.08 - distance) / .08);
                continue;
            }

            var highlightX = (x - (left + orbWidth * .35)) / radiusX;
            var highlightY = (y - (top + orbHeight * .30)) / radiusY;
            var highlight = Math.Clamp(1d - Math.Sqrt(highlightX * highlightX + highlightY * highlightY), 0d, 1d);
            var color = OrbColor(distance, highlight);
            BlendPixel(pixels, width, x, y, color, opacity);
            if (distance > .982)
                BlendPixel(pixels, width, x, y, new Rgb(255, 255, 255), opacity * .64);
        }
    }

    private static Rgb OrbColor(double distance, double highlight)
    {
        var baseColor = distance switch
        {
            < .28 => Lerp(new Rgb(217, 255, 240), new Rgb(110, 231, 183), distance / .28),
            < .70 => Lerp(new Rgb(110, 231, 183), new Rgb(79, 70, 229), (distance - .28) / .42),
            _ => Lerp(new Rgb(79, 70, 229), new Rgb(30, 27, 75), (distance - .70) / .30),
        };
        return Lerp(baseColor, new Rgb(255, 255, 255), highlight * .18);
    }

    private static void PaintProductCard(byte[] pixels, int width, int height, double left, double top, double cardWidth, double cardHeight, double degrees, double opacity)
    {
        var radians = degrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var centerX = left + cardWidth / 2;
        var centerY = top + cardHeight / 2;
        var bound = Math.Sqrt(cardWidth * cardWidth + cardHeight * cardHeight) / 2 + 2;
        var minX = Math.Max(0, (int)Math.Floor(centerX - bound));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX + bound));
        var minY = Math.Max(0, (int)Math.Floor(centerY - bound));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY + bound));

        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var localX = cos * (x - centerX) + sin * (y - centerY);
            var localY = -sin * (x - centerX) + cos * (y - centerY);
            if (Math.Abs(localX) > cardWidth / 2 || Math.Abs(localY) > cardHeight / 2)
                continue;

            var horizontal = (localX + cardWidth / 2) / cardWidth;
            var vertical = (localY + cardHeight / 2) / cardHeight;
            var color = Lerp(new Rgb(254, 243, 199), new Rgb(251, 191, 36), Math.Clamp(horizontal * .45 + vertical * .20, 0d, 1d));
            color = Lerp(color, new Rgb(251, 113, 133), Math.Max(0d, horizontal + vertical - .82) / 1.18);
            BlendPixel(pixels, width, x, y, color, opacity);
            if (Math.Abs(localX) > cardWidth / 2 - 1 || Math.Abs(localY) > cardHeight / 2 - 1)
                BlendPixel(pixels, width, x, y, new Rgb(255, 255, 255), opacity * .60);
        }
    }

    private static void PaintSparkle(byte[] pixels, int width, int height, double left, double top, double sparkleWidth, double sparkleHeight, double opacity)
    {
        var centerX = left + sparkleWidth / 2;
        var centerY = top + sparkleHeight / 2;
        var radius = Math.Min(sparkleWidth, sparkleHeight) / 2;
        PaintRadialGlow(pixels, width, height, centerX, centerY, radius * 1.2, new Rgb(251, 191, 36), opacity * .45);
        var minX = Math.Max(0, (int)Math.Floor(centerX - radius));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(centerX + radius));
        var minY = Math.Max(0, (int)Math.Floor(centerY - radius));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(centerY + radius));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = Math.Abs(x - centerX) / radius;
            var dy = Math.Abs(y - centerY) / radius;
            if (dx + dy <= 1 || (dx <= .18 && dy <= 1) || (dy <= .18 && dx <= 1))
                BlendPixel(pixels, width, x, y, new Rgb(254, 243, 199), opacity);
        }
    }

    private static void FillRoundedRect(byte[] pixels, int width, int height, double left, double top, double rectWidth, double rectHeight, double radius, Rgb color, double opacity)
    {
        var minX = Math.Max(0, (int)Math.Floor(left));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(left + rectWidth));
        var minY = Math.Max(0, (int)Math.Floor(top));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(top + rectHeight));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            if (InsideRoundedRect(x + .5, y + .5, left, top, rectWidth, rectHeight, radius))
                BlendPixel(pixels, width, x, y, color, opacity);
    }

    private static bool InsideRoundedRect(double x, double y, double left, double top, double rectWidth, double rectHeight, double radius)
    {
        if (radius <= 0)
            return x >= left && x <= left + rectWidth && y >= top && y <= top + rectHeight;
        var clippedRadius = Math.Min(radius, Math.Min(rectWidth, rectHeight) / 2);
        var closestX = Math.Clamp(x, left + clippedRadius, left + rectWidth - clippedRadius);
        var closestY = Math.Clamp(y, top + clippedRadius, top + rectHeight - clippedRadius);
        var dx = x - closestX;
        var dy = y - closestY;
        return dx * dx + dy * dy <= clippedRadius * clippedRadius;
    }

    private static void DrawText(byte[] pixels, int width, int height, string text, double left, double top, double maxWidth, double maxHeight, Rgb color, double opacity)
    {
        var normalized = text.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || maxWidth < 5 || maxHeight < 7)
            return;
        var scale = Math.Max(1, (int)Math.Floor(Math.Min(maxHeight / 7d, maxWidth / Math.Max(1, normalized.Length * 6d))));
        var textWidth = normalized.Length * 6 * scale - scale;
        var x = left + Math.Max(0, (maxWidth - textWidth) / 2);
        foreach (var character in normalized)
        {
            var glyph = Glyph(character);
            for (var row = 0; row < glyph.Length; row++)
            for (var column = 0; column < glyph[row].Length; column++)
                if (glyph[row][column] == '1')
                    FillRect(pixels, width, height, x + column * scale, top + row * scale, scale, scale, color, opacity);
            x += 6 * scale;
        }
    }

    private static string[] Glyph(char character) => character switch
    {
        'A' => ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        'B' => ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        'C' => ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        'D' => ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        'E' => ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        'F' => ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        'G' => ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
        'H' => ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        'I' => ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
        'J' => ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
        'K' => ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        'L' => ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        'M' => ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        'N' => ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        'O' => ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        'P' => ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        'Q' => ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        'R' => ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        'S' => ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        'T' => ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        'U' => ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        'V' => ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        'W' => ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
        'X' => ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        'Y' => ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        'Z' => ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
        '0' => ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
        '1' => ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        '2' => ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        '3' => ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        '4' => ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        '5' => ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        '6' => ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        '7' => ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        '8' => ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        '9' => ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        ' ' => ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
        _ => ["11111", "10001", "00110", "00100", "00100", "00000", "00100"],
    };

    private static void FillRect(byte[] pixels, int width, int height, double left, double top, double rectWidth, double rectHeight, Rgb color, double opacity)
    {
        var minX = Math.Max(0, (int)Math.Floor(left));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(left + rectWidth));
        var minY = Math.Max(0, (int)Math.Floor(top));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(top + rectHeight));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
            BlendPixel(pixels, width, x, y, color, opacity);
    }

    private static void BlendPixel(byte[] pixels, int width, int x, int y, Rgb color, double opacity)
    {
        var offset = (y * width + x) * 3;
        var alpha = Math.Clamp(opacity, 0d, 1d);
        pixels[offset] = (byte)Math.Round(pixels[offset] * (1 - alpha) + color.R * alpha);
        pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * (1 - alpha) + color.G * alpha);
        pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * (1 - alpha) + color.B * alpha);
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, Rgb color)
    {
        var offset = (y * width + x) * 3;
        pixels[offset] = color.R;
        pixels[offset + 1] = color.G;
        pixels[offset + 2] = color.B;
    }

    private static Rgb Lerp(Rgb from, Rgb to, double amount) => new(
        (byte)Math.Round(from.R + (to.R - from.R) * Math.Clamp(amount, 0d, 1d)),
        (byte)Math.Round(from.G + (to.G - from.G) * Math.Clamp(amount, 0d, 1d)),
        (byte)Math.Round(from.B + (to.B - from.B) * Math.Clamp(amount, 0d, 1d)));

    private readonly record struct Rgb(byte R, byte G, byte B);
}
