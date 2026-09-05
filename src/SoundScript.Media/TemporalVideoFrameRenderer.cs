using System.Globalization;
using System.Text;

namespace SoundScript.Media;

/// <summary>
/// Rasterizes already-evaluated temporal samples for a video encoder. This
/// class never inspects a VisualTimeline or recomputes automation: its input is
/// solely the immutable export plan built from StateAt(t).
/// </summary>
public static class TemporalVideoFrameRenderer
{
    public static void WritePpmFrames(
        TemporalVideoExportPlan plan,
        string outputDirectory,
        int width = 640,
        int height = 360)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        if (width is < 2 or > 3840 || height is < 2 or > 2160 || width % 2 != 0 || height % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Video frame dimensions must be even and within 2–3840 × 2–2160.");

        Directory.CreateDirectory(outputDirectory);
        for (var index = 0; index < plan.Samples.Count; index++)
        {
            var bytes = RenderPpm(plan.Samples[index], width, height);
            File.WriteAllBytes(Path.Combine(outputDirectory, $"frame-{index:D6}.ppm"), bytes);
        }
    }

    /// <summary>Renders one supplied state to portable pixmap bytes for FFmpeg's image2 input.</summary>
    public static byte[] RenderPpm(TemporalVideoSample sample, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        var pixels = new byte[checked(width * height * 3)];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var amount = 0.86 + 0.14 * (1 - (double)y / Math.Max(1, height - 1));
            SetPixel(pixels, width, x, y, (byte)(11 * amount), (byte)(13 * amount), (byte)(18 * amount));
        }

        foreach (var element in sample.Elements)
            PaintElement(pixels, width, height, element);

        var result = new byte[header.Length + pixels.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(pixels, 0, result, header.Length, pixels.Length);
        return result;
    }

    private static void PaintElement(byte[] pixels, int width, int height, TemporalVideoElement element)
    {
        var hash = StableHash(element.Name);
        var opacity = Math.Clamp(GetProperty(element, "opacity", 1m), 0m, 1m);
        if (opacity <= 0)
            return;

        var centerX = (int)Math.Round(GetProperty(element, "x", 0.5m) * width);
        var centerY = (int)Math.Round(GetProperty(element, "y", 0.5m) * height);
        if (!HasProperty(element, "x"))
            centerX = (int)(width * (0.27 + (hash & 0xff) / 255d * 0.46));
        if (!HasProperty(element, "y"))
            centerY = (int)(height * (0.32 + ((hash >> 8) & 0xff) / 255d * 0.38));

        var color = ColorFor(hash);
        var radius = Math.Clamp((int)Math.Round(GetProperty(element, "radius", GetProperty(element, "size", 64m))), 8, Math.Min(width, height) / 2);
        var name = element.Name.ToLowerInvariant();

        if (name.Contains("circle", StringComparison.Ordinal))
        {
            FillCircle(pixels, width, height, centerX, centerY, radius, color, opacity);
            return;
        }

        if (name.Contains("sparkle", StringComparison.Ordinal))
        {
            FillDiamond(pixels, width, height, centerX, centerY, radius, color, opacity);
            return;
        }

        var halfWidth = Math.Clamp((int)Math.Round(GetProperty(element, "width", radius * 2m)), 16, width / 2);
        var halfHeight = Math.Clamp((int)Math.Round(GetProperty(element, "height", radius)), 16, height / 3);
        FillRect(pixels, width, height, centerX - halfWidth / 2, centerY - halfHeight / 2, halfWidth, halfHeight, color, opacity);
    }

    private static void FillCircle(byte[] pixels, int width, int height, int centerX, int centerY, int radius, Rgb color, decimal opacity)
    {
        var radiusSquared = radius * radius;
        for (var y = Math.Max(0, centerY - radius); y < Math.Min(height, centerY + radius); y++)
        for (var x = Math.Max(0, centerX - radius); x < Math.Min(width, centerX + radius); x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            if (dx * dx + dy * dy <= radiusSquared)
                BlendPixel(pixels, width, x, y, color, opacity);
        }
    }

    private static void FillDiamond(byte[] pixels, int width, int height, int centerX, int centerY, int radius, Rgb color, decimal opacity)
    {
        for (var y = Math.Max(0, centerY - radius); y < Math.Min(height, centerY + radius); y++)
        for (var x = Math.Max(0, centerX - radius); x < Math.Min(width, centerX + radius); x++)
            if (Math.Abs(x - centerX) + Math.Abs(y - centerY) <= radius)
                BlendPixel(pixels, width, x, y, color, opacity);
    }

    private static void FillRect(byte[] pixels, int width, int height, int left, int top, int rectWidth, int rectHeight, Rgb color, decimal opacity)
    {
        for (var y = Math.Max(0, top); y < Math.Min(height, top + rectHeight); y++)
        for (var x = Math.Max(0, left); x < Math.Min(width, left + rectWidth); x++)
            BlendPixel(pixels, width, x, y, color, opacity);
    }

    private static void BlendPixel(byte[] pixels, int width, int x, int y, Rgb color, decimal opacity)
    {
        var offset = (y * width + x) * 3;
        var alpha = (double)opacity;
        pixels[offset] = (byte)Math.Round(pixels[offset] * (1 - alpha) + color.R * alpha);
        pixels[offset + 1] = (byte)Math.Round(pixels[offset + 1] * (1 - alpha) + color.G * alpha);
        pixels[offset + 2] = (byte)Math.Round(pixels[offset + 2] * (1 - alpha) + color.B * alpha);
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, byte red, byte green, byte blue)
    {
        var offset = (y * width + x) * 3;
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
    }

    private static bool HasProperty(TemporalVideoElement element, string name) =>
        element.Properties.Any(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static decimal GetProperty(TemporalVideoElement element, string name, decimal fallback) =>
        element.Properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))?.Value
        ?? fallback;

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
                hash = hash * 31 + character;
            return hash;
        }
    }

    private static Rgb ColorFor(int hash) => new(
        (byte)(90 + (hash & 0x3f)),
        (byte)(130 + ((hash >> 6) & 0x5f)),
        (byte)(160 + ((hash >> 13) & 0x5f)));

    private readonly record struct Rgb(byte R, byte G, byte B);
}
