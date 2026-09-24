using System.Globalization;
using System.Security.Cryptography;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.RuntimeLab.Tests;

public sealed class RuntimeTests
{
    private const string Source = """
        param intensity = 0.25
        param xpos = 200
        perform expressive
        tempo 120
        track cue { gain intensity C4 q E4 q G4 h }
        track fixed { gain 0.1 C3 q }
        visual "indicator" for 4s { shape circle set x xpos set y 300 set width 100 set height 100 set opacity intensity }
        visual "fixed" for 4s at 0s { shape rectangle set x 1100 set opacity 1 }
        """;

    [Fact]
    public void CompileOnceAndTenThousandUpdatesPreserveTemplateAndOldSnapshot()
    {
        var runtime = RuntimeProgram.Compile(Source);
        var template = runtime.TemplateForTests;
        var track = template.Statements.OfType<TrackNode>().First();
        var note = track.Body.OfType<NoteNode>().First();
        var original = runtime.Bind();
        var wav = original.RenderAudio();
        var json = Json(original);
        for (var i = 0; i < 10000; i++)
        {
            runtime.Set("intensity", i % 2 == 0 ? .9m : .25m);
            runtime.Set("xpos", i % 2 == 0 ? 900m : 200m);
            _ = runtime.Bind();
        }
        Assert.Same(template, runtime.TemplateForTests);
        Assert.Equal(.25, track.Body.OfType<GainNode>().Single().Value);
        Assert.Same(note, runtime.Bind().ProgramForTests.Statements.OfType<TrackNode>().First().Body.OfType<NoteNode>().First());
        Assert.Equal(1, runtime.Counters.Tokenizations);
        Assert.Equal(1, runtime.Counters.Parses);
        Assert.Equal(1, runtime.Counters.TimelineCompilations);
        runtime.Set("intensity", .9m);
        runtime.Set("xpos", 900m);
        Assert.Equal(wav, original.RenderAudio());
        Assert.Equal(json, Json(original));
        Assert.NotEqual(Hash(wav), Hash(runtime.RenderAudio()));
        Assert.NotEqual(json, Json(runtime.Bind()));
    }

    [Fact]
    public void StateCycleAndIndependentCompilesProduceIdenticalWavJsonSvgHashes()
    {
        var runtime = RuntimeProgram.Compile(Source);
        var normal = Digests(runtime.Bind());
        runtime.Set("intensity", .9m); runtime.Set("xpos", 900m);
        var critical = Digests(runtime.Bind());
        Assert.All(normal.Zip(critical), pair => Assert.NotEqual(pair.First, pair.Second));
        Assert.Equal(critical, Digests(runtime.Bind()));
        runtime.Set("xpos", 200m); runtime.Set("intensity", .25m);
        Assert.Equal(normal, Digests(runtime.Bind()));
        Assert.Equal(normal, Digests(RuntimeProgram.Compile(Source).Bind()));
    }

    [Fact]
    public void RuntimeStatesMatchIndependentlyCompiledLiteralProductionPrograms()
    {
        var runtime = RuntimeProgram.Compile(Source);
        foreach (var (intensity, xpos) in new[] { (.25m, 200m), (.55m, 550m), (.9m, 900m), (0m, 0m), (1m, 1280m) })
        {
            runtime.Set("intensity", intensity); runtime.Set("xpos", xpos);
            var literal = Source[Source.IndexOf("perform", StringComparison.Ordinal)..]
                .Replace("intensity", intensity.ToString(CultureInfo.InvariantCulture))
                .Replace("xpos", xpos.ToString(CultureInfo.InvariantCulture));
            var media = SoundScriptEngine.Compile(literal).CompileMedia();
            Assert.Equal(media.RenderAudio(), runtime.RenderAudio());
            foreach (var seconds in new[] { 0d, 1, 2, 3.999, 4, 5 })
            {
                var time = TimeSpan.FromSeconds(seconds);
                Assert.Equal(TemporalVisualJson.Serialize(media.SceneAt(time)), TemporalVisualJson.Serialize(runtime.SceneAt(time)));
                Assert.Equal(TemporalSvgRenderer.Render(media.SceneAt(time)), TemporalSvgRenderer.Render(runtime.SceneAt(time)));
            }
        }
    }

    [Fact]
    public void ParametersChangeOnlyIntendedAudioDynamicsAndVisualFields()
    {
        var runtime = RuntimeProgram.Compile(Source);
        var normal = runtime.Bind();
        var notes = AstToNoteEventAdapter.Adapt(normal.ProgramForTests).Tracks;
        var wav = normal.RenderAudio();
        runtime.Set("xpos", 900m);
        Assert.Equal(wav, runtime.RenderAudio());
        var moved = runtime.SceneAt(TimeSpan.FromSeconds(2));
        Assert.Equal(900m, moved.Primitives[0].Left + moved.Primitives[0].Width / 2);
        Assert.Equal(.25m, moved.Primitives[0].Opacity);
        runtime.Set("intensity", .9m);
        var loud = runtime.Bind();
        var louderNotes = AstToNoteEventAdapter.Adapt(loud.ProgramForTests).Tracks;
        Assert.Equal(notes["fixed"], louderNotes["fixed"]);
        Assert.Equal(notes["cue"].Select(n => (n.FrequencyHz, n.StartTimeSeconds, n.DurationSeconds)),
            louderNotes["cue"].Select(n => (n.FrequencyHz, n.StartTimeSeconds, n.DurationSeconds)));
        Assert.All(notes["cue"].Zip(louderNotes["cue"]), pair => Assert.True(pair.Second.Velocity > pair.First.Velocity));
        Assert.True(Rms(loud.RenderAudio()) > Rms(wav));
        var after = loud.SceneAt(TimeSpan.FromSeconds(2));
        Assert.Equal(.9m, after.Primitives[0].Opacity);
        Assert.Equal(TemporalVisualJson.Serialize(new(2, [moved.Primitives[1]])), TemporalVisualJson.Serialize(new(2, [after.Primitives[1]])));
        Assert.Equal(moved.Primitives[0].Left, after.Primitives[0].Left);
        Assert.Equal(wav.Length, loud.RenderAudio().Length);
    }

    [Theory]
    [InlineData("unknown", .5)]
    [InlineData("intensity", -0.01)]
    [InlineData("intensity", 1.01)]
    [InlineData("xpos", 12801)]
    public void InvalidUpdatesAreAtomic(string name, double number)
    {
        var runtime = RuntimeProgram.Compile(Source);
        var before = runtime.Bind();
        Assert.ThrowsAny<ArgumentException>(() => runtime.Set(name, (decimal)number));
        Assert.Same(before, runtime.Bind());
        Assert.Equal(0, runtime.Revision);
    }

    [Theory]
    [InlineData("0.9")]
    [InlineData("0.9 } track injected { C4 w }")]
    [InlineData(1)]
    [InlineData(.9)]
    [InlineData(true)]
    [InlineData(null)]
    public void WrongTypesAndSourceInjectionAreRejected(object? value)
    {
        var runtime = RuntimeProgram.Compile(Source);
        Assert.Throws<ArgumentException>(() => runtime.Set("intensity", value));
        Assert.Equal(0, runtime.Revision);
    }

    [Theory]
    [InlineData("param value = 0.5 param value = 0.7")]
    [InlineData("param value")]
    [InlineData("param value =")]
    [InlineData("param value = \"0.5\"")]
    [InlineData("param value = 0.5")]
    [InlineData("param value = 0.5 tempo value")]
    [InlineData("param value = 0.5 let copy = value")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set x value + 1 }")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set size value }")]
    [InlineData("param value = 0.5 track t { gain value C4 q }")]
    [InlineData("param value = 0.5 perform expressive track t { loop 2 { gain value C4 q } }")]
    [InlineData("param value = 1.1 visual \"v\" for 1s { set opacity value }")]
    [InlineData("param value = 0.5 visual \"v\" for 1s { set opacity value } visual \"v\" for 1s")]
    public void InvalidOrUnsupportedTemplatesFailClosed(string source)
    {
        Assert.ThrowsAny<ArgumentException>(() => RuntimeProgram.Compile(source));
    }

    [Fact]
    public void UnknownSourceVariableAndImportsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => RuntimeProgram.Compile("visual \"v\" for 1s { set x typo }"));
        Assert.Throws<NotSupportedException>(() => RuntimeProgram.Compile("import \"other.ss\""));
    }

    [Theory]
    [InlineData("x", -12800, 12800)]
    [InlineData("y", -7200, 7200)]
    [InlineData("width", 8, 1280)]
    [InlineData("height", 8, 720)]
    [InlineData("rotation", -360, 360)]
    [InlineData("opacity", 0, 1)]
    public void PropertyRangesAreTypedAndEnforced(string property, int minimum, int maximum)
    {
        var runtime = RuntimeProgram.Compile($"param value = {minimum} visual \"v\" for 1s {{ set {property} value }}");
        var schema = Assert.Single(runtime.Parameters);
        Assert.Equal(typeof(decimal), schema.ValueType);
        Assert.Equal((decimal)minimum, schema.Minimum); Assert.Equal((decimal)maximum, schema.Maximum);
        runtime.Set("value", (decimal)maximum);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Set("value", minimum - 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Set("value", maximum + 1m));
    }

    [Fact]
    public void SameNamePropertyAndParameterAndCommentsAreHandledAsTokens()
    {
        var runtime = RuntimeProgram.Compile("""
            // param bogus = 5
            param x = 200 /* ignored references: set x injected */
            visual "x" for 1s { shape text text "param x" set x x }
            """);
        runtime.Set("x", 700m);
        var primitive = Assert.Single(runtime.SceneAt(TimeSpan.Zero).Primitives);
        Assert.Equal(700m, primitive.Left + primitive.Width / 2);
    }

    [Fact]
    public void SharedParameterUsesIntersectionAndFailedDefaultIsRejected()
    {
        var runtime = RuntimeProgram.Compile("param value = 0.5 visual \"v\" for 1s { set x value set opacity value }");
        Assert.Equal(1m, runtime.Parameters[0].Maximum);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Set("value", 2m));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeProgram.Compile("param value = 20 visual \"v\" for 1s { set width value set opacity value }"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("tempo 120 C4 q D4 q")]
    [InlineData("let xpos = 200 + 30 marker start = 1s visual \"v\" for 2s at start { set x xpos animate opacity 0 -> 1 over 2s }")]
    [InlineData("tempo 120 -> 180 over 1 bars track t { C4 q E4 q } sync audio visual \"a\" for 1s wait 1s visual \"b\" for 2s visual \"a\" for 1s at 0s")]
    public void UnchangedProgramsAreByteAndBehaviorEquivalent(string source)
    {
        var runtime = RuntimeProgram.Compile(source);
        var media = SoundScriptEngine.Compile(source).CompileMedia();
        Assert.Equal(media.RenderAudio(), runtime.RenderAudio());
        foreach (var time in new[] { 0d, .5, 1, 2, 3, 4, 10 })
            Assert.Equal(TemporalVisualJson.Serialize(media.SceneAt(TimeSpan.FromSeconds(time))),
                TemporalVisualJson.Serialize(runtime.SceneAt(TimeSpan.FromSeconds(time))));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SceneAt(TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void SnapshotOutputOwnershipAndNoOpSetAreSafe()
    {
        var runtime = RuntimeProgram.Compile(Source);
        var frame = runtime.Bind();
        runtime.Set("intensity", .25m);
        Assert.Same(frame, runtime.Bind());
        var bytes = frame.RenderAudio(); bytes[0] = 0;
        Assert.Equal((byte)'R', frame.RenderAudio()[0]);
        Assert.Empty(frame.SceneAt(TimeSpan.FromSeconds(4)).Primitives);
    }

    [Fact]
    public void MultipleGainSlotsUseStructuralAddressesInsteadOfDefaultValueMatching()
    {
        var runtime = RuntimeProgram.Compile("""
            param first = 0.25 param second = 0.25
            perform expressive
            track alpha { gain first C4 q gain second E4 q }
            track beta { gain first G4 h }
            track literal { gain 0.25 C3 q }
            """);
        runtime.Set("first", .8m); runtime.Set("second", .6m);
        var tracks = runtime.Bind().ProgramForTests.Statements.OfType<TrackNode>().ToArray();
        Assert.Equal(new[] { .8, .6 }, tracks[0].Body.OfType<GainNode>().Select(g => g.Value));
        Assert.Equal(.8, tracks[1].Body.OfType<GainNode>().Single().Value);
        Assert.Equal(.25, tracks[2].Body.OfType<GainNode>().Single().Value);
        Assert.All(runtime.TemplateForTests.Statements.OfType<TrackNode>().SelectMany(t => t.Body.OfType<GainNode>()),
            g => Assert.Equal(.25, g.Value));
    }

    [Theory]
    [InlineData("x", 700)]
    [InlineData("y", 450)]
    [InlineData("width", 400)]
    [InlineData("height", 300)]
    [InlineData("rotation", 90)]
    [InlineData("opacity", 1)]
    public void EveryApprovedVisualBindingMatchesProductionGeometry(string property, int value)
    {
        var source = $"visual \"v\" for 1s {{ shape rectangle set {property} value }}";
        var defaultValue = property == "opacity" ? "0" : "100";
        var runtime = RuntimeProgram.Compile($"param value = {defaultValue} " + source);
        runtime.Set("value", (decimal)value);
        var literal = SoundScriptEngine.Compile(source.Replace("value", value.ToString(CultureInfo.InvariantCulture))).CompileMedia();
        Assert.Equal(TemporalVisualJson.Serialize(literal.SceneAt(TimeSpan.Zero)), TemporalVisualJson.Serialize(runtime.SceneAt(TimeSpan.Zero)));
    }

    [Fact]
    public void BindingPreservesOutOfOrderIntervalsOverlapWaitAndTempoRamp()
    {
        const string source = """
            param xpos = 200
            tempo 120 -> 180 over 1 bars
            track cue { C4 q E4 q G4 h }
            sync audio
            visual "late" for 1s at 3s { set x xpos }
            visual "first" for 1s { set x xpos }
            wait 1s
            visual "afterWait" for 1s { animate opacity 0 -> 1 over 1s set x xpos }
            visual "overlap" for 1s at 0.5s
            """;
        var runtime = RuntimeProgram.Compile(source);
        var wav = runtime.RenderAudio();
        runtime.Set("xpos", 700m);
        var literal = SoundScriptEngine.Compile(source[source.IndexOf("tempo", StringComparison.Ordinal)..].Replace("xpos", "700")).CompileMedia();
        foreach (var seconds in new[] { 0d, .5, .999, 1, 1.5, 2, 2.5, 3, 4, 10 })
        {
            var time = TimeSpan.FromSeconds(seconds);
            Assert.Equal(TemporalVisualJson.Serialize(literal.SceneAt(time)), TemporalVisualJson.Serialize(runtime.SceneAt(time)));
        }
        Assert.Equal(wav, runtime.RenderAudio());
        Assert.Equal(literal.RenderAudio(), runtime.RenderAudio());
        Assert.Equal(1, runtime.Counters.TimelineCompilations);
    }

    [Fact]
    public void CultureDoesNotChangeParameterBindingOrOutputs()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var first = RuntimeProgram.Compile(Source);
            first.Set("intensity", .8m);
            var expected = Digests(first.Bind());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = RuntimeProgram.Compile(Source);
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            second.Set("intensity", .8m);
            Assert.Equal(expected, Digests(second.Bind()));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void FailedCompilationRestoresCultureAndPlainProgramsRetainProductionBehavior()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Throws<InvalidOperationException>(() => RuntimeProgram.Compile("param value = 0.5 visual \"v\" for 1s { set x value nonsense }"));
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            const string source = "perform expressive track t { gain 0.5 C4 q }";
            var expected = Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.Compile(source));
            var actual = Assert.Throws<InvalidOperationException>(() => RuntimeProgram.Compile(source));
            Assert.Equal(expected.Message, actual.Message);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static string Json(RuntimeFrame frame) => TemporalVisualJson.Serialize(frame.SceneAt(TimeSpan.FromSeconds(2)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string[] Digests(RuntimeFrame frame) => [Hash(frame.RenderAudio()),
        Hash(System.Text.Encoding.UTF8.GetBytes(Json(frame))),
        Hash(System.Text.Encoding.UTF8.GetBytes(TemporalSvgRenderer.Render(frame.SceneAt(TimeSpan.FromSeconds(2)))))];
    private static double Rms(byte[] wav)
    {
        using var stream = new MemoryStream(wav);
        return Math.Sqrt(WavReader.ReadMono(stream).Average(v => (double)v * v));
    }
}
