using System.Globalization;
using System.Xml.Linq;
using SoundScript.Media;
using Xunit;

namespace SoundScript.Tests;

public class MediaSvgTests
{
    [Fact]
    public void SecurityGoldenContainsOnlyEscapedTextAndAttributes()
    {
        var scene = new TemporalVisualScene(0, new[] {
            new TemporalVisualPrimitive("\" onload=\"alert(1)", "generic", "<script>alert(1)</script> <img src=x onerror=alert(1)> < > & \" ' 日本語", 0, 0, 1280, 100, 1, 0)
        });
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden/v14-security.svg"));
        Assert.Equal(expected, TemporalSvgRenderer.Render(scene));
        _ = XDocument.Parse(expected);
    }

    [Theory]
    [InlineData("rectangle")]
    [InlineData("roundedRectangle")]
    [InlineData("ellipse")]
    [InlineData("circle")]
    [InlineData("triangle")]
    [InlineData("line")]
    [InlineData("arrow")]
    [InlineData("ring")]
    [InlineData("text")]
    public void EveryCanonicalShapePaintsExistingGeometry(string kind)
    {
        var style = kind == "text" ? "text \"HELLO\" fill \"#123456\"" :
            kind is "line" or "ring" ? "stroke \"#abcdef\" strokeWidth 3" : "fill \"#123456\" stroke \"#abcdef\" strokeWidth 3";
        var scene = SoundScriptEngine.Compile($$"""
            visual "item" for 2s { shape {{kind}} {{style}} set x 300 set y 200 set width 120 set height 80 set opacity 0.5 set rotation 45 }
            """).CompileMedia().SceneAt(TimeSpan.FromSeconds(1));
        var xml = TemporalSvgRenderer.Render(scene);
        var document = XDocument.Parse(xml);
        Assert.Equal("0 0 1280 720", document.Root!.Attribute("viewBox")!.Value);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var primitive = Assert.Single(scene.Primitives);
        var paths = document.Descendants(ns + "path").ToArray();
        Assert.Equal(primitive.Paths!.Count, paths.Length);
        Assert.NotEmpty(paths);
        Assert.Equal("0.5", document.Descendants(ns + "g").Single().Attribute("opacity")!.Value);
        for (var i = 0; i < paths.Length; i++)
        {
            Assert.Equal(primitive.Paths[i].Stroke, paths[i].Attribute("stroke")!.Value);
            Assert.Equal(primitive.Paths[i].Fill, paths[i].Attribute("fill")!.Value);
            Assert.Contains(primitive.Paths[i].Points[0].X.ToString("R", CultureInfo.InvariantCulture), paths[i].Attribute("d")!.Value);
        }
        Assert.Equal(xml, TemporalSvgRenderer.Render(scene));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("\" onload=\"alert(1)")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("< > & \" ' 日本語 😀")]
    public void UntrustedLabelsStayText(string payload)
    {
        var scene = new TemporalVisualScene(0, new[] { new TemporalVisualPrimitive(payload, "generic", payload, 0, 0, 100, 50, 1, 0) });
        var svg = TemporalSvgRenderer.Render(scene);
        var xml = XDocument.Parse(svg);
        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName is "script" or "img");
        Assert.DoesNotContain(xml.Descendants().Attributes(), a => a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(payload, xml.Descendants().Single(e => e.Name.LocalName == "text").Value);
        Assert.Contains("&", svg);
    }

    [Theory]
    [InlineData("url(javascript:alert(1))")]
    [InlineData("\" onload=\"alert(1)")]
    [InlineData("url(https://example.org/image)")]
    public void ArbitraryPaintsCannotLoadActiveContent(string paint)
    {
        var p = new TemporalVisualPrimitive("x", "rectangle", "", 0, 0, 1, 1, 1, 0)
        { Paths = new[] { new TemporalShapePath(new[] { new TemporalPoint(0, 0) }, true, paint, "none", 0) } };
        Assert.Throws<ArgumentException>(() => TemporalSvgRenderer.Render(new(0, new[] { p })));
    }

    [Fact]
    public void EmptySvgIsStableGolden() => Assert.Equal(
        "<svg viewBox=\"0 0 1280 720\" width=\"1280\" height=\"720\" role=\"img\" xmlns=\"http://www.w3.org/2000/svg\" />",
        TemporalSvgRenderer.Render(new(0, Array.Empty<TemporalVisualPrimitive>())));
}
