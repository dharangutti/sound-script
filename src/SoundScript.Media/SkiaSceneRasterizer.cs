using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace SoundScript.Media;

/// <summary>
/// CPU raster adapter for the Playground Canvas presentation profile. Shared
/// paths have already resolved coordinates, rotation, colors and layer order.
/// This boundary never evaluates source properties or timeline state.
/// </summary>
internal static class SkiaSceneRasterizer
{
    private static readonly ConcurrentDictionary<int, Lazy<SKTypeface>> Fonts = new();

    public static byte[] RenderPpm(TemporalVisualScene scene, int width, int height)
    {
        // Explicit RGBA/sRGB prevents platform-native BGRA or color-profile drift.
        using var colorSpace = SKColorSpace.CreateSrgb();
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace));
        using var canvas = new SKCanvas(bitmap);
        Background(canvas, width, height);
        canvas.Scale(width / 1280f, height / 720f);
        foreach (var primitive in scene.Primitives)
        {
            if (primitive.Opacity <= 0) continue;
            if (primitive.Paths is { } paths)
                DrawPaths(canvas, paths, (float)primitive.Opacity);
            else
                DrawLegacy(canvas, primitive);
        }
        canvas.Flush();
        var rgba = new byte[bitmap.ByteCount];
        Marshal.Copy(bitmap.GetPixels(), rgba, 0, rgba.Length);
        var header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        var result = new byte[header.Length + checked(width * height * 3)];
        header.CopyTo(result, 0);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var source = y * bitmap.RowBytes + x * 4;
            var destination = header.Length + (y * width + x) * 3;
            // Background is opaque: all composited pixels have alpha 255.
            result[destination] = rgba[source];
            result[destination + 1] = rgba[source + 1];
            result[destination + 2] = rgba[source + 2];
        }
        return result;
    }

    private static SKColor Color(string value) => SKColor.Parse(value);
    private static SKColor Alpha(SKColor color, float alpha) => color.WithAlpha((byte)Math.Clamp(MathF.Round(color.Alpha * alpha), 0, 255));
    private static SKPaint Paint(SKColor color, float opacity = 1) => new() {
        IsAntialias = true, Color = Alpha(color, opacity), BlendMode = SKBlendMode.SrcOver,
        StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round,
    };

    private static void Background(SKCanvas canvas, int width, int height)
    {
        using var paint = Paint(SKColors.White);
        using (var gradient = SKShader.CreateLinearGradient(new(0, 0), new(width, height),
            [Color("#10192a"), Color("#17122c"), Color("#0e1c26")], [0, .55f, 1], SKShaderTileMode.Clamp))
        {
            paint.Shader = gradient;
            canvas.DrawRect(0, 0, width, height, paint);
        }
        void Glow(float x, float y, float radius, SKColor color)
        {
            using var gradient = SKShader.CreateRadialGradient(new(x, y), radius,
                [color, color.WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp);
            paint.Shader = gradient;
            canvas.DrawRect(0, 0, width, height, paint);
        }
        Glow(width * .75f, height * .15f, Math.Max(width, height) * .3f, Alpha(Color("#6ee7b7"), .18f));
        Glow(width * .18f, height * .9f, Math.Max(width, height) * .38f, Alpha(Color("#818cf8"), .25f));
        paint.Shader = null;
        paint.Color = Alpha(SKColors.White, .07f * .28f);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        paint.StrokeCap = SKStrokeCap.Butt;
        var cell = Math.Max(12, (int)Math.Floor(Math.Min(width / 1280d, height / 720d) * 32 + .5));
        using var grid = new SKPath();
        for (var x = 0; x <= width; x += cell) { grid.MoveTo(x + .5f, 0); grid.LineTo(x + .5f, height); }
        for (var y = 0; y <= height; y += cell) { grid.MoveTo(0, y + .5f); grid.LineTo(width, y + .5f); }
        canvas.DrawPath(grid, paint);
    }

    private static void DrawPaths(SKCanvas canvas, IReadOnlyList<TemporalShapePath> paths, float opacity)
    {
        using var paint = Paint(SKColors.White, opacity);
        foreach (var source in paths)
        {
            if (source.Points.Count == 0) continue;
            using var path = new SKPath { FillType = SKPathFillType.Winding };
            path.MoveTo((float)source.Points[0].X, (float)source.Points[0].Y);
            foreach (var point in source.Points.Skip(1)) path.LineTo((float)point.X, (float)point.Y);
            if (source.Closed) path.Close();
            if (source.Closed && source.Fill != "none")
            {
                paint.Style = SKPaintStyle.Fill;
                paint.Color = Alpha(Color(source.Fill), opacity);
                canvas.DrawPath(path, paint);
            }
            if (source.Stroke != "none" && source.StrokeWidth > 0)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = (float)source.StrokeWidth;
                paint.Color = Alpha(Color(source.Stroke), opacity);
                canvas.DrawPath(path, paint);
            }
        }
    }

    // These named-visual decorations mirror visual-scene-renderer.js. Explicit
    // shapes/text always take DrawPaths, including the established bitmap font.
    private static void DrawLegacy(SKCanvas canvas, TemporalVisualPrimitive p)
    {
        var x = (float)p.Left; var y = (float)p.Top;
        var w = Math.Max(1, (float)p.Width); var h = Math.Max(1, (float)p.Height);
        var opacity = (float)Math.Clamp(p.Opacity, 0, 1);
        var kind = p.Kind.ToLowerInvariant();
        canvas.Save();
        canvas.RotateDegrees((float)p.RotationDegrees, x + w / 2, y + h / 2);
        using var paint = Paint(SKColors.White, opacity);
        using var path = kind == "circle" ? Ellipse(x, y, w, h) : RoundedRect(x, y, w, h,
            kind == "intro" ? h / 2 : kind == "product" ? 14 : 8);

        if (kind != "sparkle")
        {
            switch (kind)
            {
                case "intro": paint.Color = Alpha(Color("#0f182b"), .72f * opacity); break;
                case "product":
                    paint.Shader = SKShader.CreateLinearGradient(new(x, y), new(x + w, y + h),
                        [Color("#fef3c7"), Color("#fbbf24"), Color("#fb7185")], [0, .4f, 1], SKShaderTileMode.Clamp);
                    break;
                case "circle":
                    paint.Shader = SKShader.CreateTwoPointConicalGradient(new(x + w * .35f, y + h * .3f), Math.Max(1, Math.Min(w, h) * .025f),
                        new(x + w / 2, y + h / 2), Math.Max(w, h) / 2,
                        [Color("#d9fff0"), Color("#6ee7b7"), Color("#4f46e5"), Color("#1e1b4b")], [0, .28f, .7f, 1], SKShaderTileMode.Clamp);
                    break;
                default: paint.Color = Alpha(Color("#0f131a"), .8f * opacity); break;
            }
            if (kind is "intro" or "product" or "circle")
            {
                var blur = kind == "intro" ? 38 : 40;
                var offset = kind == "intro" ? 14 : kind == "product" ? 20 : 22;
                var alpha = kind == "intro" ? .24f : kind == "product" ? .34f : .38f;
                DrawWithShadow(canvas, paint, p => canvas.DrawPath(path, p), blur, offset, Alpha(SKColors.Black, alpha));
            }
            else canvas.DrawPath(path, paint);
            paint.Shader?.Dispose(); paint.Shader = null;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            paint.Color = Alpha(kind is "intro" or "product" or "circle" ? SKColors.White : Color("#a5f3fc"),
                opacity * (kind == "intro" ? .4f : kind == "product" ? .6f : kind == "circle" ? .64f : 1));
            canvas.DrawPath(path, paint);
            if (kind == "circle")
            {
                using var halo = Ellipse(x - 10, y - 10, w + 20, h + 20);
                paint.StrokeWidth = 16;
                paint.Color = Alpha(Color("#6ee7b7"), .18f * opacity);
                canvas.DrawPath(halo, paint);
            }
        }
        var text = p.Label;
        var fontSize = kind switch {
            "intro" => Math.Clamp(h * .48f, 22, 42),
            "product" => Math.Clamp(h * .3f, 20, 42),
            "circle" => Math.Clamp(Math.Min(w, h) * .38f, 28, 64),
            "sparkle" => Math.Clamp(Math.Max(w, h) * .85f, 34, 96),
            _ => Math.Clamp(h * .36f, 18, 32),
        };
        var weight = kind switch { "intro" => 800, "product" => 900, "sparkle" => 400, _ => 700 };
        var textColor = kind switch {
            "intro" => Color("#eef6ff"), "product" => Color("#071b17"),
            "circle" => Alpha(SKColors.White, .75f), "sparkle" => Color("#fef3c7"), _ => Color("#e8ecf4"),
        };
        if (string.IsNullOrEmpty(text)) text = kind switch {
            "intro" => "A visual idea begins", "product" => "PRODUCT", "sparkle" => "✦",
            "circle" => "", _ => string.IsNullOrEmpty(p.Name) ? "visual" : p.Name,
        };
        Text(canvas, text, x, y, w, h, fontSize, weight, Alpha(textColor, opacity), kind == "sparkle");
        canvas.Restore();
    }

    private static void DrawWithShadow(SKCanvas canvas, SKPaint paint, Action<SKPaint> draw, float blur, float offsetY, SKColor color)
    {
        // Canvas shadowBlur/offset are device-space values, unaffected by CTM.
        // Skia image filters are local-space, so cancel scale/rotation for offsets.
        var matrix = canvas.TotalMatrix;
        if (!matrix.TryInvert(out var inverse)) { draw(paint); return; }
        var origin = inverse.MapPoint(0, 0);
        var offset = inverse.MapPoint(0, offsetY) - origin;
        var sx = MathF.Sqrt(matrix.ScaleX * matrix.ScaleX + matrix.SkewY * matrix.SkewY);
        var sy = MathF.Sqrt(matrix.SkewX * matrix.SkewX + matrix.ScaleY * matrix.ScaleY);
        using var shadow = SKImageFilter.CreateDropShadowOnly(offset.X, offset.Y, blur / 2 / sx, blur / 2 / sy, color);
        paint.ImageFilter = shadow;
        draw(paint);
        paint.ImageFilter = null;
        draw(paint);
    }

    private static void Text(SKCanvas canvas, string text, float x, float y, float w, float h, float size, int weight, SKColor color, bool sparkle)
    {
        if (string.IsNullOrEmpty(text) || w <= 18) return;
        var typeface = Fonts.GetOrAdd(weight, key => new Lazy<SKTypeface>(() => ResolveFont(key))).Value;
        // Symbols may live in a separate system font; resolve the complete label
        // before shaping so advances, kerning and the drawn glyphs use one face.
        using var fallback = !typeface.ContainsGlyphs(text)
            ? SKFontManager.Default.MatchCharacter(text.EnumerateRunes().First(rune => !typeface.ContainsGlyphs(rune.ToString())).Value) : null;
        using var font = new SKFont(fallback ?? typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true };
        using var shaper = new SKShaper(font.Typeface);
        var measured = shaper.Shape(text, font).Width;
        using var paint = Paint(color);
        canvas.Save();
        canvas.Translate(x + w / 2, y + h / 2);
        canvas.Scale(Math.Min(1, (w - 18) / Math.Max(measured, .001f)), 1);
        // Canvas middle is the middle of the font's x-height, not the midpoint
        // of ascent/descent (which shifts Segoe UI labels down by ~6px at 42px).
        var baseline = font.Metrics.XHeight / 2;
        void Draw(SKPaint p) => canvas.DrawShapedText(shaper, text, 0, baseline, SKTextAlign.Center, font, p);
        if (sparkle) DrawWithShadow(canvas, paint, Draw, 20, 0, Alpha(Color("#fbbf24"), .95f));
        else Draw(paint);
        canvas.Restore();
    }

    private static SKTypeface ResolveFont(int weight)
    {
        var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        var families = OperatingSystem.IsWindows() ? new[] { "Segoe UI" } :
            OperatingSystem.IsMacOS() ? new[] { ".AppleSystemUIFont", "Helvetica Neue" } : new[] { "DejaVu Sans", "Noto Sans" };
        foreach (var family in families)
        {
            var face = SKFontManager.Default.MatchFamily(family, style);
            if (face is not null && face.FamilyName.Contains(family.TrimStart('.'), StringComparison.OrdinalIgnoreCase)) return face;
            face?.Dispose();
        }
        // SIL Open Font License fallback, also works on servers without fonts.
        using var stream = typeof(SkiaSceneRasterizer).Assembly.GetManifestResourceStream(
            "SoundScript.Media.Fonts.NotoSans-" + (weight >= 600 ? "Bold" : "Regular") + ".ttf")!;
        return SKTypeface.FromStream(stream);
    }

    private static SKPath Ellipse(float x, float y, float w, float h)
    {
        var path = new SKPath(); path.AddOval(SKRect.Create(x, y, w, h)); return path;
    }

    private static SKPath RoundedRect(float x, float y, float w, float h, float radius)
    {
        // Canvas uses quadratic corners here, not circular round-rect arcs.
        var r = Math.Min(Math.Max(0, radius), Math.Min(w, h) / 2);
        var path = new SKPath();
        path.MoveTo(x + r, y); path.LineTo(x + w - r, y); path.QuadTo(x + w, y, x + w, y + r);
        path.LineTo(x + w, y + h - r); path.QuadTo(x + w, y + h, x + w - r, y + h);
        path.LineTo(x + r, y + h); path.QuadTo(x, y + h, x, y + h - r);
        path.LineTo(x, y + r); path.QuadTo(x, y, x + r, y); path.Close();
        return path;
    }
}
