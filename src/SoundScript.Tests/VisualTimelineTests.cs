using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Visual;
using SoundScript.Media;
using SoundScript.Wave.Io;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

/// <summary>
/// These tests deliberately query semantic instants rather than frames. A
/// future renderer may sample the same timeline at 24, 30, or 60 FPS without
/// changing any assertion here.
/// </summary>
public class VisualTimelineTests
{
    [Fact]
    public void Interpret_BuildsSequentialIntervalsWaitsOverlaysAndAudioAnchor()
    {
        var timeline = Compile("""
            sync audio
            visual "intro" for 4s
            wait 1s
            visual "product" for 5s
            visual "badge" for 2s at 6s
            """);

        Assert.Equal(TimeSpan.FromSeconds(10), timeline.Duration);
        var anchor = Assert.Single(timeline.AudioSyncPoints);
        Assert.Equal(TimeSpan.Zero, anchor.Time);

        Assert.Collection(timeline.Visuals,
            intro =>
            {
                Assert.Equal("intro", intro.Name);
                Assert.Equal(TimeSpan.Zero, intro.Start);
                Assert.Equal(TimeSpan.FromSeconds(4), intro.End);
            },
            product =>
            {
                Assert.Equal("product", product.Name);
                Assert.Equal(TimeSpan.FromSeconds(5), product.Start);
                Assert.Equal(TimeSpan.FromSeconds(10), product.End);
            },
            badge =>
            {
                Assert.Equal("badge", badge.Name);
                Assert.Equal(TimeSpan.FromSeconds(6), badge.Start);
                Assert.Equal(TimeSpan.FromSeconds(8), badge.End);
            });
    }

    [Fact]
    public void StateAt_EvaluatesArbitraryTimesUsingHalfOpenIntervalsAndAutomation()
    {
        var timeline = Compile("""
            visual "intro" for 4s
            wait 1s
            visual "product" for 5s
            visual "circle" for 5s at 0s {
                animate radius 20 -> 200 over 3s
            }
            """);

        Assert.Equal(["intro", "circle"], NamesAt(timeline, 0));

        var atOnePointFive = timeline.StateAt(TimeSpan.FromSeconds(1.5));
        Assert.Equal(["intro", "circle"], atOnePointFive.Elements.Select(element => element.Name));
        Assert.Equal(110m, Property(atOnePointFive, "circle", "radius"));

        // intro ends at exactly 4s; circle remains active and its curve is clamped.
        Assert.Equal(["circle"], NamesAt(timeline, 4));
        Assert.Equal(200m, Property(timeline.StateAt(TimeSpan.FromSeconds(4)), "circle", "radius"));
        Assert.Equal(["circle"], NamesAt(timeline, 4.5));
        Assert.Equal(["product"], NamesAt(timeline, 5));
        Assert.Equal(["product"], NamesAt(timeline, 8.75));
        Assert.Empty(timeline.StateAt(TimeSpan.FromSeconds(10)).Elements);
    }

    [Fact]
    public void Interpret_ExplicitPlacementDoesNotMoveTheNarrativeCursor()
    {
        var timeline = Compile("""
            visual "background" for 10s
            visual "badge" for 2s at 4s
            visual "outro" for 1s
            """);

        var outro = Assert.Single(timeline.Visuals.Where(visual => visual.Name == "outro"));
        Assert.Equal(TimeSpan.FromSeconds(10), outro.Start);
        Assert.Equal(TimeSpan.FromSeconds(11), outro.End);
        Assert.Equal(["background", "badge"], NamesAt(timeline, 4));
    }

    [Fact]
    public void StateAtAudioBeat_UsesTheSharedTempoMapClock()
    {
        var timeline = Compile("""
            visual "intro" for 4s
            wait 1s
            visual "product" for 5s
            """);
        var tempoMap = new TempoAutomationMap();
        tempoMap.SetTempo(0, 120);

        // At 120 BPM, score beat 10 maps to the same 5-second instant as the product cue.
        var state = timeline.StateAtAudioBeat(10, tempoMap);

        Assert.Equal(TimeSpan.FromSeconds(5), state.Time);
        Assert.Equal(["product"], state.Elements.Select(element => element.Name));
    }

    [Fact]
    public void Interpret_IsDeterministicAndDoesNotDisturbMidiInterpretation()
    {
        const string source = """
            tempo 120
            track melody { C4 q }
            visual "intro" for 1.5s
            wait 0.5s
            visual "product" for 2s
            """;

        var first = Compile(source);
        var second = Compile(source);

        Assert.Equal(
            first.StateAt(TimeSpan.FromSeconds(1.75)).Elements.Select(Describe),
            second.StateAt(TimeSpan.FromSeconds(1.75)).Elements.Select(Describe));
        Assert.Equal(first.Visuals.Select(Describe), second.Visuals.Select(Describe));

        var audio = SoundScript.Midi.Interpreter.Interpret(Parse(source));
        var melody = Assert.Single(audio.Tracks);
        Assert.Single(melody.Notes);
        Assert.Equal(60, melody.Notes[0].MidiNumber);
    }

    [Fact]
    public void Parse_RejectsAutomationThatOutlastsItsVisual()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Parse("""
            visual "circle" for 3s {
                animate radius 20 -> 200 over 4s
            }
            """));

        Assert.Contains("lasts longer", exception.Message);
    }

    [Fact]
    public void VideoExportPlan_SamplesTheAuthoritativeStateAtFunctionAtThirtyFps()
    {
        var timeline = Compile("""
            visual "intro" for 1s
            visual "overlay" for 0.5s at 0.5s {
                animate opacity 0 -> 1 over 0.5s
            }
            """);

        var plan = TemporalVideoExportPlanBuilder.Build(timeline);

        Assert.Equal(30, plan.FramesPerSecond);
        Assert.Equal(1d, plan.DurationSeconds);
        Assert.Equal(30, plan.Samples.Count);
        Assert.Equal(0d, plan.Samples[0].TimeSeconds);
        Assert.Equal(TimeSpan.FromSeconds(1d / 30d).TotalSeconds, plan.Samples[1].TimeSeconds, 12);
        Assert.Equal(TimeSpan.FromSeconds(29d / 30d).TotalSeconds, plan.Samples[^1].TimeSeconds, 12);
        Assert.Equal(["intro", "overlay"], plan.Samples[15].Elements.Select(element => element.Name));
        Assert.Equal(0m, PlanProperty(plan.Samples[15], "overlay", "opacity"));
    }

    [Fact]
    public void VideoExportPlan_AlternateRatesObserveEquivalentTemporalStateWithoutMutation()
    {
        var timeline = Compile("""
            visual "circle" for 2s {
                animate radius 20 -> 200 over 2s
            }
            """);

        var atThirty = TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(30));
        var atSixty = TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(60));

        var thirtyAtOneSecond = Assert.Single(atThirty.Samples, sample => sample.TimeSeconds == 1d);
        var sixtyAtOneSecond = Assert.Single(atSixty.Samples, sample => sample.TimeSeconds == 1d);
        Assert.Equal(thirtyAtOneSecond.Elements.Select(Describe), sixtyAtOneSecond.Elements.Select(Describe));
        Assert.Equal(110m, PlanProperty(thirtyAtOneSecond, "circle", "radius"));
        Assert.Equal(110m, Property(timeline.StateAt(TimeSpan.FromSeconds(1)), "circle", "radius"));
        Assert.Empty(timeline.StateAt(TimeSpan.FromSeconds(2)).Elements);
    }

    [Fact]
    public void VideoExportPlan_RejectsUnsupportedOutputRate()
    {
        var timeline = Compile("visual \"still\" for 1s");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(25)));
    }

    [Fact]
    public void VisualScene_UsesTheSameProjectionForDirectStateAndExportSamples()
    {
        var timeline = Compile("""
            visual "intro" for 3s
            visual "circle" for 8s at 0s {
                animate radius 28 -> 170 over 3s
                animate opacity 0.35 -> 1 over 1.5s
            }
            visual "product" for 4s at 3s
            """);
        var plan = TemporalVisualSceneBuilder.Build(
            TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(30)));

        foreach (var seconds in new[] { 0d, 1.5d, 3d, 4.5d, 5d, 7.5d })
        {
            var direct = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(seconds)));
            var sampled = Assert.Single(plan.Samples, sample => Math.Abs(sample.TimeSeconds - seconds) < 1e-9);
            Assert.Equal(direct.TimeSeconds, sampled.TimeSeconds);
            Assert.Equal(direct.Primitives.Select(Describe), sampled.Primitives.Select(Describe));
        }

        var arbitrary = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(8.75)));
        Assert.Equal(0.0, arbitrary.Primitives.Count);
    }

    [Fact]
    public void VisualScene_AppliesAnimatedOpacityToEveryPresentationKind()
    {
        var timeline = Compile("""
            visual "outro" for 2s {
                animate opacity 1 -> 0 over 2s
            }
            """);

        var scene = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(1)));

        var primitive = Assert.Single(scene.Primitives);
        Assert.Equal("generic", primitive.Kind);
        Assert.Equal(0.5m, primitive.Opacity);
    }

    [Fact]
    public void TemporalAudioRenderer_IsDeterministicAndFitsTheVisualDuration()
    {
        var program = Parse("""
            tempo 120
            track music { C4 h E4 h }
            """);

        var first = TemporalAudioRenderer.RenderToWavBytes(program, TimeSpan.FromSeconds(1.25));
        var second = TemporalAudioRenderer.RenderToWavBytes(program, TimeSpan.FromSeconds(1.25));

        Assert.Equal(first, second);
        using var stream = new MemoryStream(first, writable: false);
        Assert.Equal((int)(1.25 * WavWriter.SampleRate), WavReader.ReadMono(stream).Length);
    }

    [Fact]
    public void VideoFrameRenderer_RasterizesSuppliedSnapshotsWithoutChangingTheTimeline()
    {
        var timeline = Compile("""
            visual "circle" for 1s {
                animate radius 8 -> 12 over 1s
                animate opacity 0.5 -> 1 over 1s
            }
            visual "sparkle" for 1s at 0s
            """);
        var plan = TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(24));
        var before = timeline.StateAt(TimeSpan.Zero).Elements.Select(Describe).ToArray();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"soundscript-ppm-{Guid.NewGuid():N}");

        try
        {
            TemporalVideoFrameRenderer.WritePpmFrames(plan, outputDirectory, width: 32, height: 24);

            var firstFrame = File.ReadAllBytes(Path.Combine(outputDirectory, "frame-000000.ppm"));
            Assert.StartsWith("P6\n32 24\n255\n", System.Text.Encoding.ASCII.GetString(firstFrame, 0, 13));
            Assert.Equal(13 + 32 * 24 * 3, firstFrame.Length);
            Assert.Equal(plan.Samples.Count, Directory.GetFiles(outputDirectory, "*.ppm").Length);
            Assert.NotEqual(firstFrame.Skip(13).First(), firstFrame.Skip(13).Last());
            Assert.Equal(before, timeline.StateAt(TimeSpan.Zero).Elements.Select(Describe));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void FfmpegWebmExporter_BuildsWebmArgumentsAtTheRenderingBoundary()
    {
        var timeline = Compile("visual \"still\" for 1s");
        var plan = TemporalVideoExportPlanBuilder.Build(timeline, new TemporalVideoExportSettings(30));

        var arguments = FfmpegWebmExporter.BuildEncodeArguments("frames", "audio.wav", "clip.webm", plan);

        Assert.Contains("-framerate", arguments);
        Assert.Contains("30", arguments);
        Assert.Contains("libvpx-vp9", arguments);
        Assert.Contains("libopus", arguments);
        Assert.Equal("clip.webm", arguments[^1]);
    }

    private static VisualTimeline Compile(string source) => VisualInterpreter.Interpret(Parse(source));

    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static IEnumerable<string> NamesAt(VisualTimeline timeline, double seconds) =>
        timeline.StateAt(TimeSpan.FromSeconds(seconds)).Elements.Select(element => element.Name);

    private static decimal Property(VisualState state, string visualName, string propertyName) =>
        Assert.Single(Assert.Single(state.Elements.Where(element => element.Name == visualName)).Properties
            .Where(property => property.Property == propertyName)).Value;

    private static decimal PlanProperty(TemporalVideoSample sample, string visualName, string propertyName) =>
        Assert.Single(Assert.Single(sample.Elements, element => element.Name == visualName).Properties,
            property => property.Name == propertyName).Value;

    private static string Describe(TemporalVideoElement element) =>
        $"{element.Name}:{string.Join(",", element.Properties.Select(property => $"{property.Name}={property.Value}"))}";

    private static string Describe(TemporalVisualPrimitive primitive) =>
        $"{primitive.Name}:{primitive.Kind}:{primitive.Label}:{primitive.Left}:{primitive.Top}:" +
        $"{primitive.Width}:{primitive.Height}:{primitive.Opacity}:{primitive.RotationDegrees}";

    private static string Describe(ScheduledVisual visual) =>
        $"{visual.Name}@{visual.Start.Ticks}:{visual.End.Ticks}:" +
        string.Join(",", visual.Automations.Select(curve =>
            $"{curve.Property}:{curve.From}:{curve.To}:{curve.Duration.Ticks}"));

    private static string Describe(VisualElementState element) =>
        $"{element.Name}@{element.Start.Ticks}:{element.End.Ticks}:" +
        string.Join(",", element.Properties.Select(property => $"{property.Property}:{property.Value}"));
}
