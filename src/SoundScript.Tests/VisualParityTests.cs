using SkiaSharp;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;
using Xunit;

namespace SoundScript.Tests;

public class VisualParityTests
{
    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    private static TemporalVisualScene Load(string source, double time) => TemporalVisualSceneBuilder.Build(
        VisualInterpreter.Interpret(ProgramLoader.Load(Path.Combine(Root, source)).Program).StateAt(TimeSpan.FromSeconds(time)));

    [Fact]
    public void ReferencesTrackPlaygroundRendererRevision()
    {
        using var metadata = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Golden/visual-parity/reference.metadata.json")));
        var source = File.ReadAllText(Path.Combine(Root, "src/SoundScript.Playground/wwwroot/js/visual-scene-renderer.js")).Replace("\r\n", "\n");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        Assert.Equal(metadata.RootElement.GetProperty("rendererSha256").GetString(), hash);
    }

    [Theory]
    [InlineData("org-chart", "examples/visual-org-chart.ssv", 4.5, .25, 1)]
    [InlineData("information-cards", "examples/visual-information-cards.ssv", 4, .25, 1)]
    // Broader bounds allow installed system font differences, while still
    // rejecting the previous rasterizer's background, text and stroke errors.
    [InlineData("legacy-cards", "tools/VisualParity/legacy.ssv", 1, 4, 5)]
    [InlineData("primitives-1", "tools/VisualParity/primitives.ssv", 1, 3, 4)]
    public void CliMatchesIndependentBrowserReference(string name, string source, double time, double meanLimit, double badPixelLimit)
    {
        using var expected = SKBitmap.Decode(Path.Combine(AppContext.BaseDirectory, "Golden/visual-parity", name + ".playground.png"));
        var actual = TemporalVideoFrameRenderer.RenderPpm(Load(source, time), expected.Width, expected.Height);
        var offset = actual.Length - expected.Width * expected.Height * 3;
        long sum = 0, bad = 0;
        for (var y = 0; y < expected.Height; y++)
        for (var x = 0; x < expected.Width; x++)
        {
            var color = expected.GetPixel(x, y);
            var i = offset + (y * expected.Width + x) * 3;
            var differences = new[] { Math.Abs(actual[i] - color.Red), Math.Abs(actual[i + 1] - color.Green), Math.Abs(actual[i + 2] - color.Blue) };
            sum += differences.Sum();
            if (differences.Max() > 16) bad++;
        }
        Assert.InRange(sum / (expected.Width * expected.Height * 3d), 0, meanLimit);
        Assert.InRange(bad * 100d / (expected.Width * expected.Height), 0, badPixelLimit);
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(640, 360)]
    [InlineData(640, 720)]
    public void TranslucentPathsUseSourceOverInSourceOrder(int width, int height)
    {
        TemporalVisualPrimitive Rect(string color, decimal opacity) => new("box", "rectangle", "", 0, 0, 1280, 720, opacity, 0) {
            Paths = [new([new(0, 0), new(1280, 0), new(1280, 720), new(0, 720)], true, color, "none", 0)]
        };
        var background = Rect("#ffffff", 1);
        var red = Rect("#ff0000", .5m); var blue = Rect("#0000ff", .5m);
        var image = TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(0, [background, red, blue]), width, height);
        var pixel = Pixel(image, width, height, width / 2, height / 2);
        Assert.InRange(pixel[0], 126, 129); Assert.InRange(pixel[1], 62, 65); Assert.InRange(pixel[2], 190, 193);
        var reverse = Pixel(TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(0, [background, blue, red]), width, height), width, height, width / 2, height / 2);
        Assert.True(reverse[0] > pixel[0]); Assert.True(reverse[2] < pixel[2]);
    }

    [Fact]
    public void LegacyLabelsPreserveCaseAndRasterizeDeterministicallyInParallel()
    {
        var scene = Load("tools/VisualParity/legacy.ssv", 1);
        var expected = TemporalVideoFrameRenderer.RenderPpm(scene, 640, 360);
        Parallel.For(0, 8, _ => Assert.Equal(expected, TemporalVideoFrameRenderer.RenderPpm(scene, 640, 360)));
        var lower = new TemporalVisualPrimitive("label", "generic", "Mixed Case", 100, 100, 400, 80, 1, 0);
        var upper = lower with { Label = "MIXED CASE" };
        Assert.NotEqual(TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(0, [lower]), 640, 360),
            TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(0, [upper]), 640, 360));
    }

    [Fact]
    public void TransparentShapesDoNotAlterPixelsOrState()
    {
        var scene = Load("tools/VisualParity/primitives.ssv", 1);
        var before = System.Text.Json.JsonSerializer.Serialize(scene);
        var hidden = new TemporalVisualScene(scene.TimeSeconds, scene.Primitives.Select(p => p with { Opacity = 0 }).ToArray());
        Assert.Equal(TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(1, []), 640, 360),
            TemporalVideoFrameRenderer.RenderPpm(hidden, 640, 360));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(scene));
    }

    private static byte[] Pixel(byte[] ppm, int width, int height, int x, int y) =>
        ppm.AsSpan(ppm.Length - width * height * 3 + (y * width + x) * 3, 3).ToArray();
}
