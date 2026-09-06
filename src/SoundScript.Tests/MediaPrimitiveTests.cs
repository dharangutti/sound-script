using System.Text.Json;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Visual;
using Xunit;

namespace SoundScript.Tests;

public class MediaPrimitiveTests
{
    private static SoundScript.Core.Ast.ProgramNode Parse(string source) =>
        new SoundScript.Parser.Parser(new Tokenizer(source).Tokenize()).Parse();

    private static TemporalVisualScene Scene(string body, double seconds = 0) =>
        TemporalVisualSceneBuilder.Build(VisualInterpreter.Interpret(Parse($"visual \"test\" for 2s {{ {body} }}"))
            .StateAt(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("rectangle")]
    [InlineData("roundedRectangle")]
    [InlineData("ellipse")]
    [InlineData("circle")]
    [InlineData("triangle")]
    [InlineData("line")]
    [InlineData("arrow")]
    [InlineData("ring")]
    [InlineData("text text \"HELLO 123!\"")]
    public void Shapes_ReachBothSceneAndCliRenderer(string declaration)
    {
        var program = Parse($"visual \"shape demo\" for 2s {{ shape {declaration} }}");
        var timeline = VisualInterpreter.Interpret(program);
        var state = timeline.StateAt(TimeSpan.FromSeconds(1));
        var live = TemporalVisualSceneBuilder.Build(state);
        var export = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline));
        Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(export.Samples[30]));
        Assert.NotEmpty(live.Primitives[0].Paths!);
        var image = TemporalVideoFrameRenderer.RenderPpm(live, 128, 72);
        var blank = TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(1, []), 128, 72);
        Assert.False(image.SequenceEqual(blank));
        Assert.Equal(image, TemporalVideoFrameRenderer.RenderPpm(live, 128, 72));
    }

    [Theory]
    [InlineData("shape star")]
    [InlineData("fill \"#ff0000\"")]
    [InlineData("shape rectangle shape circle")]
    [InlineData("shape rectangle fill \"red\"")]
    [InlineData("shape rectangle stroke \"#fff\"")]
    [InlineData("shape rectangle fill \"#12GG12\"")]
    [InlineData("shape rectangle fill \"#ff0000\" FILL \"none\"")]
    [InlineData("shape line fill \"#ff0000\"")]
    [InlineData("shape ring strokeWidth 129")]
    [InlineData("shape text text \"HELLO\" fontSize 7")]
    [InlineData("shape text text \"HELLO\" fontSize 257")]
    [InlineData("shape text text \"HELLO\" stroke \"#ff0000\"")]
    [InlineData("shape text")]
    [InlineData("shape text text \"你好\"")]
    [InlineData("shape rectangle text \"HELLO\"")]
    [InlineData("shape rectangle fontSize 42")]
    public void InvalidPresentations_AreRejected(string body) =>
        Assert.Throws<InvalidOperationException>(() => Parse($"visual \"test\" for 2s {{ {body} }}"));

    [Fact]
    public void ColorsTransformsAndOutline_AreRenderedAtExpectedPositions()
    {
        var scene = Scene("shape rectangle fill \"#ff0000\" animate width 200 -> 200 over 2s animate height 100 -> 100 over 2s");
        var bytes = TemporalVideoFrameRenderer.RenderPpm(scene, 128, 72);
        Assert.Equal(new byte[] { 255, 0, 0 }, Pixel(bytes, 128, 72, 64, 36));

        var line = Scene("shape line stroke \"#ff0000\" strokeWidth 30 animate width 200 -> 200 over 2s animate rotation 90 -> 90 over 2s");
        var lineBytes = TemporalVideoFrameRenderer.RenderPpm(line, 128, 72);
        Assert.Equal(new byte[] { 255, 0, 0 }, Pixel(lineBytes, 128, 72, 64, 43));
        Assert.NotEqual(new byte[] { 255, 0, 0 }, Pixel(lineBytes, 128, 72, 71, 36));

        var ring = Scene("shape ring stroke \"#ff0000\" strokeWidth 12 animate radius 80 -> 80 over 2s");
        var blank = TemporalVideoFrameRenderer.RenderPpm(new TemporalVisualScene(0, []), 128, 72);
        Assert.Equal(Pixel(blank, 128, 72, 64, 36), Pixel(TemporalVideoFrameRenderer.RenderPpm(ring, 128, 72), 128, 72, 64, 36));
    }

    [Fact]
    public void AppearanceIsImmutableAndIndependentOfAutomation()
    {
        var timeline = VisualInterpreter.Interpret(Parse("visual \"box\" for 2s { SHAPE RECTANGLE Fill \"#FF0000\" animate width 20 -> 200 over 2s }"));
        var start = timeline.StateAt(TimeSpan.Zero);
        var middle = timeline.StateAt(TimeSpan.FromSeconds(1));
        Assert.Equal(start.Elements[0].Presentation, middle.Elements[0].Presentation);
        Assert.Equal("#ff0000", middle.Elements[0].Presentation!.Fill);
        Assert.Equal(110m, TemporalVisualSceneBuilder.Build(middle).Primitives[0].Width);
        Assert.Empty(timeline.StateAt(TimeSpan.FromSeconds(2)).Elements);
    }

    [Fact]
    public void TextUsesIdenticalGlyphGeometryInBothCasesAndFitsItsBounds()
    {
        var upper = Scene("shape text text \"READY 100%\" fontSize 48");
        var lower = Scene("shape text text \"ready 100%\" fontSize 48");
        Assert.Equal(JsonSerializer.Serialize(upper.Primitives[0].Paths), JsonSerializer.Serialize(lower.Primitives[0].Paths));
        var p = upper.Primitives[0];
        Assert.All(p.Paths!.SelectMany(path => path.Points), point =>
        {
            Assert.InRange(point.X, (double)p.Left, (double)(p.Left + p.Width));
            Assert.InRange(point.Y, (double)p.Top, (double)(p.Top + p.Height));
        });
    }

    [Fact]
    public void OldNamesAndAudioKeepTheirMeaning()
    {
        var program = Parse("visual \"rectangle\" for 2s visual \"intro\" for 2s");
        var visuals = VisualInterpreter.Interpret(program);
        Assert.Equal("generic", TemporalVisualSceneBuilder.Build(visuals.StateAt(TimeSpan.Zero)).Primitives[0].Kind);
        var intro = TemporalVisualSceneBuilder.Build(visuals.StateAt(TimeSpan.FromSeconds(2))).Primitives[0];
        Assert.Equal("intro", intro.Kind);
        Assert.Null(intro.Paths);

        const string score = "tempo 120 track shape { instrument piano C4 q E4 q G4 q C5 q }";
        var withVisuals = score + " sync audio visual \"box\" for 2s { shape rectangle fill \"#ff0000\" }";
        static byte[] Midi(string source)
        {
            using var output = new MemoryStream();
            MidiGenerator.Write(Interpreter.Interpret(Parse(source)), output);
            return output.ToArray();
        }
        Assert.Equal(Midi(score), Midi(withVisuals));
        Assert.Equal(TemporalAudioRenderer.RenderToWavBytes(Parse(score), TimeSpan.FromSeconds(2)),
            TemporalAudioRenderer.RenderToWavBytes(Parse(withVisuals), TimeSpan.FromSeconds(2)));
    }

    private static byte[] Pixel(byte[] ppm, int width, int height, int x, int y) =>
        ppm.AsSpan(ppm.Length - width * height * 3 + (y * width + x) * 3, 3).ToArray();
}
