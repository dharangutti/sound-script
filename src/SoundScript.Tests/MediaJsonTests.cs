using System.Globalization;
using System.Text.Json;
using SoundScript.Media;
using Xunit;

namespace SoundScript.Tests;

public class MediaJsonTests
{
    [Fact]
    public void VersionedJsonRoundTripsStringsNumbersPathsAndOrdering()
    {
        var label = "\"\\/\n<>&' Ελληνικά 日本語 😀" + new string('x', 10000);
        var first = new TemporalVisualPrimitive("first", "text", label, 1.25m, 2, 30, 40, .5m, 90)
        {
            Paths = new[] { new TemporalShapePath(new[] { new TemporalPoint(1.25, 2.5), new TemporalPoint(3, 4) }, false, "none", "#123456", 2.5) }
        };
        var scene = new TemporalVisualScene(1.5, new[] { first, first with { Name = "second" } });
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var json = TemporalVisualJson.Serialize(scene);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(json, TemporalVisualJson.Serialize(scene));
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal("1.0", parsed.RootElement.GetProperty("schemaVersion").GetString());
            var primitives = parsed.RootElement.GetProperty("primitives");
            Assert.Equal("first", primitives[0].GetProperty("name").GetString());
            Assert.Equal("second", primitives[1].GetProperty("name").GetString());
            Assert.Equal(label, primitives[0].GetProperty("label").GetString());
            Assert.Equal(1.25m, primitives[0].GetProperty("left").GetDecimal());
            Assert.Equal(2.5, primitives[0].GetProperty("paths")[0].GetProperty("strokeWidth").GetDouble());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void EmptySceneHasStableGoldenRepresentation() =>
        Assert.Equal("{\"schemaVersion\":\"1.0\",\"timeSeconds\":0,\"primitives\":[]}", TemporalVisualJson.Serialize(new(0, Array.Empty<TemporalVisualPrimitive>())));
}
