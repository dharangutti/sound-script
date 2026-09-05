using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Visual;
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

    private static VisualTimeline Compile(string source) => VisualInterpreter.Interpret(Parse(source));

    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static IEnumerable<string> NamesAt(VisualTimeline timeline, double seconds) =>
        timeline.StateAt(TimeSpan.FromSeconds(seconds)).Elements.Select(element => element.Name);

    private static decimal Property(VisualState state, string visualName, string propertyName) =>
        Assert.Single(Assert.Single(state.Elements.Where(element => element.Name == visualName)).Properties
            .Where(property => property.Property == propertyName)).Value;

    private static string Describe(ScheduledVisual visual) =>
        $"{visual.Name}@{visual.Start.Ticks}:{visual.End.Ticks}:" +
        string.Join(",", visual.Automations.Select(curve =>
            $"{curve.Property}:{curve.From}:{curve.To}:{curve.Duration.Ticks}"));

    private static string Describe(VisualElementState element) =>
        $"{element.Name}@{element.Start.Ticks}:{element.End.Ticks}:" +
        string.Join(",", element.Properties.Select(property => $"{property.Property}:{property.Value}"));
}
