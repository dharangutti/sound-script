using System.Globalization;
using SoundScript.Media;
using Xunit;

namespace SoundScript.Tests;

public sealed class RuntimeBindingTests
{
    public const string Source = """
        param intensity = 0.25
        param xpos = 200
        perform expressive
        tempo 120
        track cue { gain intensity C4 q rest q E4 h }
        visual "indicator" for 4s { shape circle set x xpos set width 100 set height 100 set opacity intensity }
        """;

    [Fact]
    public void TenThousandUpdatesDoNotRecompileAndOldSnapshotsRemainStable()
    {
        var runtime = SoundScriptEngine.CompileRuntime(Source);
        var old = runtime.Bind();
        var audio = old.RenderAudio();
        var midi = old.RenderMidi();
        var scene = Json(old);
        for (var i = 0; i < 10000; i++)
        {
            runtime.Set("intensity", i % 2 == 0 ? .9m : .25m);
            runtime.Set("xpos", i % 2 == 0 ? 900m : 200m);
            _ = runtime.Bind();
        }
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), runtime.Statistics);
        runtime.SetMany(new Dictionary<string, decimal> { ["intensity"] = .9m, ["xpos"] = 900m });
        Assert.Equal(audio, old.RenderAudio()); Assert.Equal(midi, old.RenderMidi()); Assert.Equal(scene, Json(old));
        Assert.False(audio.SequenceEqual(runtime.RenderAudio()));
        Assert.NotEqual(scene, Json(runtime.Bind()));
        runtime.Reset();
        Assert.Equal(audio, runtime.RenderAudio()); Assert.Equal(midi, runtime.Bind().RenderMidi());
        Assert.Equal(scene, Json(runtime.Bind()));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("hi-IN")]
    public void RuntimeCultureIsInvariantAndCallerCultureIsRestored(string culture)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var expected = SoundScriptEngine.CompileRuntime(Source).RenderAudio();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(expected, SoundScriptEngine.CompileRuntime(Source).RenderAudio());
            Assert.Equal(culture, CultureInfo.CurrentCulture.Name);
            Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.CompileRuntime("param xpos = 1 nonsense"));
            Assert.Equal(culture, CultureInfo.CurrentCulture.Name);
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.25, 200)]
    [InlineData(.55, 550)]
    [InlineData(.9, 900)]
    [InlineData(1, 1280)]
    public void BoundWavMidiJsonAndSvgMatchLiteralProduction(double level, int xpos)
    {
        var intensity = (decimal)level;
        var runtime = SoundScriptEngine.CompileRuntime(Source);
        runtime.Set("intensity", intensity); runtime.Set("xpos", (decimal)xpos);
        var literal = Source[Source.IndexOf("perform", StringComparison.Ordinal)..]
            .Replace("intensity", intensity.ToString(CultureInfo.InvariantCulture)).Replace("xpos", xpos.ToString(CultureInfo.InvariantCulture));
        var compiled = SoundScriptEngine.Compile(literal);
        var frame = runtime.Bind();
        Assert.Equal(compiled.RenderWave(), frame.RenderWave());
        Assert.Equal(compiled.RenderMidi(), frame.RenderMidi());
        Assert.Equal(compiled.CompileMedia().RenderAudio(), frame.RenderAudio());
        foreach (var seconds in new[] { 0d, 2, 3.999, 4, 10 })
        {
            var time = TimeSpan.FromSeconds(seconds);
            Assert.Equal(TemporalVisualJson.Serialize(compiled.CompileMedia().SceneAt(time)), TemporalVisualJson.Serialize(frame.SceneAt(time)));
            Assert.Equal(TemporalSvgRenderer.Render(compiled.CompileMedia().SceneAt(time)), TemporalSvgRenderer.Render(frame.SceneAt(time)));
        }
    }

    [Fact]
    public async Task ConcurrentUpdatesAndSnapshotRendersAreIsolated()
    {
        var runtime = SoundScriptEngine.CompileRuntime(Source);
        var snapshot = runtime.Bind();
        var midi = snapshot.RenderMidi(); var audio = snapshot.RenderAudio();
        var renders = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            runtime.SetMany(new Dictionary<string, decimal> { ["intensity"] = .5m, ["xpos"] = 500m });
            Assert.Equal(midi, snapshot.RenderMidi()); Assert.Equal(audio, snapshot.RenderAudio());
        }));
        await Task.WhenAll(renders);
        Assert.Equal(1, runtime.Revision);
        Assert.Equal(.5m, runtime.Get("intensity"));
    }

    [Fact]
    public void ValidationIsTypedAtomicAndNoOpUpdatesPreserveSnapshot()
    {
        var runtime = SoundScriptEngine.CompileRuntime(Source);
        Assert.Equal(typeof(decimal), runtime.Parameters[0].ValueType);
        Assert.Equal(.25m, runtime.Get("intensity"));
        var snapshot = runtime.Bind();
        runtime.Set("intensity", .25m);
        Assert.Same(snapshot, runtime.Bind());
        Assert.Throws<ArgumentException>(() => runtime.Set("unknown", .5m));
        Assert.Throws<ArgumentException>(() => runtime.Set("intensity", "1 } track injected { C4 q }"));
        Assert.Throws<ArgumentException>(() => runtime.Set("intensity", (object)0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Set("intensity", 1.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SetMany(new Dictionary<string, decimal> { ["xpos"] = 900, ["intensity"] = -1 }));
        Assert.Same(snapshot, runtime.Bind()); Assert.Equal(0, runtime.Revision); Assert.Equal(200m, runtime.Get("xpos"));
    }

    [Theory]
    [InlineData("param value = 0.5 param value = 0.6")]
    [InlineData("param value = 0.5 let value = 1")]
    [InlineData("let value = 1 param value = 0.5")]
    [InlineData("param value = 0.5")]
    [InlineData("param value = \"0.5\"")]
    [InlineData("param value = 0.5 tempo value")]
    [InlineData("param value = 0.5 visual \"v\" for value")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set x value + 1 }")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set size value }")]
    [InlineData("param value = 0.5 track t { gain value C4 q }")]
    [InlineData("param value = 0.5 perform expressive track t { loop 2 { gain value C4 q } }")]
    [InlineData("param value = 1.1 visual \"v\" for 1s { set opacity value }")]
    [InlineData("param value = 20 visual \"v\" for 1s { set width value set opacity value }")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set x value } visual \"v\" for 1s")]
    [InlineData("visual \"v\" for 1s { set x unknown }")]
    public void InvalidRuntimeSourcesFailClosed(string source) =>
        Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.CompileRuntime(source));

    [Theory]
    [InlineData("x", -12800, 12800)]
    [InlineData("y", -7200, 7200)]
    [InlineData("width", 8, 1280)]
    [InlineData("height", 8, 720)]
    [InlineData("rotation", -360, 360)]
    [InlineData("opacity", 0, 1)]
    public void AllVisualTargetsAreValidatedAndRendered(string property, int min, int max)
    {
        var source = $"visual \"v\" for 1s {{ shape rectangle set {property} value }}";
        var runtime = SoundScriptEngine.CompileRuntime($"param value = {min} " + source);
        Assert.Equal((decimal)min, runtime.Parameters[0].Minimum); Assert.Equal((decimal)max, runtime.Parameters[0].Maximum);
        runtime.Set("value", (decimal)max);
        var literal = SoundScriptEngine.Compile(source.Replace("value", max.ToString(CultureInfo.InvariantCulture))).CompileMedia();
        Assert.Equal(TemporalVisualJson.Serialize(literal.SceneAt(TimeSpan.Zero)), TemporalVisualJson.Serialize(runtime.SceneAt(TimeSpan.Zero)));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Set("value", max + 1m));
    }

    [Fact]
    public void ContextualParamPreservesExistingNamesConstantsAndMarkers()
    {
        const string source = "let param = 200 marker start = 1s track param { C4 q } visual \"param\" for 1s at start { set x param }";
        var staticMedia = SoundScriptEngine.Compile(source).CompileMedia();
        var runtime = SoundScriptEngine.CompileRuntime(source);
        Assert.Empty(runtime.Parameters);
        Assert.Equal(staticMedia.RenderAudio(), runtime.RenderAudio());
        Assert.Equal(TemporalVisualJson.Serialize(staticMedia.SceneAt(TimeSpan.FromSeconds(1))), TemporalVisualJson.Serialize(runtime.SceneAt(TimeSpan.FromSeconds(1))));
        var x = SoundScriptEngine.CompileRuntime("param x = 200 track visual { C4 q } visual \"x\" for 1s { set x x }");
        x.Set("x", 700m);
        Assert.Equal(700m, x.SceneAt(TimeSpan.Zero).Primitives[0].Left + x.SceneAt(TimeSpan.Zero).Primitives[0].Width / 2);
        Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.Compile(Source));
    }

    [Fact]
    public void VisualParameterLeavesAudioUntouchedAndFileIsNeverReopened()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ss");
        File.WriteAllText(path, Source);
        SoundScriptRuntimeProgram runtime;
        try { runtime = SoundScriptEngine.CompileRuntimeFile(path); }
        finally { File.Delete(path); }
        var audio = runtime.RenderAudio();
        runtime.Set("xpos", 900m);
        Assert.Equal(audio, runtime.RenderAudio());
        Assert.Throws<NotSupportedException>(() => SoundScriptEngine.CompileRuntime("import \"x.ss\""));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SceneAt(TimeSpan.FromTicks(-1)));
    }

    private static string Json(SoundScriptRuntimeSnapshot frame) => TemporalVisualJson.Serialize(frame.SceneAt(TimeSpan.FromSeconds(2)));
}
