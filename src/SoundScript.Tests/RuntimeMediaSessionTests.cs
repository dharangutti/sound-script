using SoundScript;
using SoundScript.Playground;
using Xunit;

namespace SoundScript.Tests;

public sealed class RuntimeMediaSessionTests
{
    [Fact]
    public void GeometryExampleBindsEverySupportedTargetAndResets()
    {
        var session = new RuntimeMediaSession();
        session.Compile(RuntimeMediaSession.GeometrySource);
        var runtime = session.Runtime!;
        var audio = session.Audio.ToArray();
        var svg = session.Svg;
        Assert.Equal(7, runtime.Parameters.Count);
        session.Apply(new Dictionary<string, string>
        {
            ["volume"] = "0.7", ["xpos"] = "800", ["ypos"] = "250",
            ["width"] = "320", ["height"] = "180", ["angle"] = "45", ["opacity"] = "0.6"
        });
        Assert.NotEqual(audio, session.Audio);
        Assert.NotEqual(svg, session.Svg);
        var tile = Assert.Single(runtime.Bind().SceneAt(TimeSpan.FromSeconds(2)).Primitives);
        Assert.Equal(45m, tile.RotationDegrees);
        Assert.Equal(0.6m, tile.Opacity);
        Assert.Equal(320m, tile.Width);
        Assert.Equal(180m, tile.Height);
        Assert.Equal(640m, tile.Left);
        Assert.Equal(160m, tile.Top);
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), runtime.Statistics);
        session.Reset();
        Assert.Equal(audio, session.Audio);
        Assert.Equal(svg, session.Svg);
    }

    [Fact]
    public void CompileApplyAndResetRenderSnapshotsWithoutRecompiling()
    {
        var session = new RuntimeMediaSession();
        session.Compile(RuntimeMediaSession.MonitoringSource);
        var runtime = Assert.IsType<SoundScriptRuntimeProgram>(session.Runtime);
        var initialAudio = session.Audio.ToArray();
        var initialSvg = session.Svg;
        var initialJson = session.Json;
        Assert.NotEmpty(initialAudio);
        Assert.Contains("indicator", initialSvg);
        Assert.Contains("150", initialJson);

        session.Apply(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["intensity"] = "0.9",
            ["xpos"] = "900"
        });

        Assert.Same(runtime, session.Runtime);
        Assert.Equal(.9m, runtime.Get("intensity"));
        Assert.Equal(900m, runtime.Get("xpos"));
        Assert.Equal(1, runtime.Revision);
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), runtime.Statistics);
        Assert.NotEqual(initialAudio, session.Audio);
        Assert.NotEqual(initialJson, session.Json);
        Assert.NotEqual(initialSvg, session.Svg);
        Assert.Contains("850", session.Json);
        Assert.Contains("0.9", session.Json);

        session.Reset();

        Assert.Same(runtime, session.Runtime);
        Assert.Equal(.25m, runtime.Get("intensity"));
        Assert.Equal(200m, runtime.Get("xpos"));
        Assert.Equal(2, runtime.Revision);
        Assert.Equal(initialAudio, session.Audio);
        Assert.Equal(initialSvg, session.Svg);
        Assert.Equal(initialJson, session.Json);
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), runtime.Statistics);
    }

    [Theory]
    [InlineData("2", "300", "Expected")]
    [InlineData("0,9", "300", "dot")]
    [InlineData("0.8 } track injected { C4 q", "300", "decimal")]
    public void RejectedApplyLeavesCurrentValuesAndRenderedStateUnchanged(string intensity, string xpos, string message)
    {
        var session = new RuntimeMediaSession();
        session.Compile(RuntimeMediaSession.MonitoringSource);
        var runtime = session.Runtime!;
        var audio = session.Audio.ToArray();
        var svg = session.Svg;
        var json = session.Json;

        var error = Assert.ThrowsAny<ArgumentException>(() => session.Apply(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["intensity"] = intensity,
            ["xpos"] = xpos
        }));

        Assert.Contains(message, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(runtime, session.Runtime);
        Assert.Equal(0, runtime.Revision);
        Assert.Equal(.25m, runtime.Get("intensity"));
        Assert.Equal(200m, runtime.Get("xpos"));
        Assert.Equal(audio, session.Audio);
        Assert.Equal(svg, session.Svg);
        Assert.Equal(json, session.Json);
    }

    [Fact]
    public void FailedCompileClearsSessionAndUninitializedActionsFailPredictably()
    {
        var session = new RuntimeMediaSession();
        session.Compile(RuntimeMediaSession.MonitoringSource);
        Assert.NotEmpty(session.Audio);

        Assert.Throws<InvalidOperationException>(() => session.Compile("param broken = 0.5 visual \"v\" for 1s { set x broken + 1 }"));

        Assert.Null(session.Runtime);
        Assert.Empty(session.Audio);
        Assert.Empty(session.Svg);
        Assert.Empty(session.Json);
        Assert.Throws<InvalidOperationException>(() => session.Apply(new Dictionary<string, string>()));
        Assert.Throws<InvalidOperationException>(session.Reset);
    }
}
