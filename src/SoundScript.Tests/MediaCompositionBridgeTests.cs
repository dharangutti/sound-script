using System.Text.Json;
using System.Text.RegularExpressions;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Midi;
using SoundScript.Playground;
using SoundScript.Visual;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public class MediaCompositionBridgeTests
{
    private static ProgramNode Parse(string source) => AudioVisualUseCaseTests.Parse(source);

    [Theory]
    [InlineData("x", "640")]
    [InlineData("y", "0.5")]
    [InlineData("width", "320")]
    [InlineData("height", "160")]
    [InlineData("size", "80")]
    [InlineData("radius", "40")]
    [InlineData("rotation", "270")]
    [InlineData("opacity", "0")]
    public void Set_LowersToExistingConstantAst(string property, string value)
    {
        var compact = Parse($"visual \"test\" for 1250ms {{ set {property} {value} }}");
        var original = Parse($"visual \"test\" for 1250ms {{ animate {property} {value} -> {value} over 1250ms }}");
        Assert.Equal(Assert.IsType<VisualNode>(original.Statements[0]).Automations,
            Assert.IsType<VisualNode>(compact.Statements[0]).Automations);
    }

    [Theory]
    [InlineData("set x 1 set X 2")]
    [InlineData("set x 1 animate X 0 -> 1 over 1s")]
    [InlineData("animate x 0 -> 1 over 1s set X 1")]
    [InlineData("set fontSize 24")]
    [InlineData("set zIndex 2")]
    [InlineData("set unknown 3")]
    [InlineData("set x")]
    [InlineData("set opacity \"bad\"")]
    public void Set_RejectsAmbiguousOrUnsupportedProperties(string body) =>
        Assert.Throws<InvalidOperationException>(() => Parse($"visual \"test\" for 1s {{ {body} }}"));

    [Theory]
    [MemberData(nameof(AudioVisualUseCaseTests.Cases), MemberType = typeof(AudioVisualUseCaseTests))]
    public void CompactTemplates_PreserveExistingSceneSemanticsAndCatalogSource(string key)
    {
        var source = AudioVisualUseCaseTests.Source(key);
        var expanded = Regex.Replace(source, "visual \"[^\"]+\" for ([0-9.]+)s[^{}]*\\{([^}]+)\\}", block =>
            Regex.Replace(block.Value, @"set (\w+) ([0-9.]+)", setting =>
                $"animate {setting.Groups[1].Value} {setting.Groups[2].Value} -> {setting.Groups[2].Value} over {block.Groups[1].Value}s"));
        Assert.NotEqual(source, expanded);
        var compact = VisualInterpreter.Interpret(Parse(source));
        var original = VisualInterpreter.Interpret(Parse(expanded));
        foreach (var seconds in new[] { 0, 0.75, 2, 3.125, 4, 5.99, 6 })
            Assert.Equal(JsonSerializer.Serialize(original.StateAt(TimeSpan.FromSeconds(seconds))),
                JsonSerializer.Serialize(compact.StateAt(TimeSpan.FromSeconds(seconds))));
        var preset = VisualPresetCatalog.All.Single(p => p.Key == "visual-" + key);
        Assert.Equal(source, preset.Source);
        Assert.NotEqual("Getting started", preset.Category);
        Assert.Equal(PlaygroundOutputRail.AudioVisual, PlaygroundPresetCatalog.TryGet(preset.Key)!.OutputRail);
    }

    [Fact]
    public void WaveClockBridge_AllowsSpeechEffectsAndMatchesScheduledNotes()
    {
        var program = Parse("""
            tempo 120
            tempo 120 -> 60 over 2 bars
            track music { rest:4 C4 q }
            track narration { rest:4 speak "go" seed=7 }
            effect delay time=0.125 feedback=0.1 mix=0.08
            visual "caption" for 12s { shape text text "GO" }
            """);
        Assert.Throws<NotSupportedException>(() => Interpreter.Interpret(program));
        var map = TemporalAudioRenderer.BuildTempoMap(program);
        var adapted = AstToNoteEventAdapter.Adapt(program);
        var time = map.BeatsToMilliseconds(0, 4) / 1000;
        Assert.Equal(time, adapted.Tracks["music"][0].StartTimeSeconds, 6);
        Assert.Equal(time, adapted.SpeakTimings[0].StartTimeSeconds, 6);
        var timeline = VisualInterpreter.Interpret(program);
        var exactTime = TimeSpan.FromTicks(decimal.ToInt64(decimal.Round(
            (decimal)map.BeatsToMilliseconds(0, 4) * TimeSpan.TicksPerMillisecond, 0, MidpointRounding.AwayFromZero)));
        Assert.Equal(JsonSerializer.Serialize(timeline.StateAt(exactTime)),
            JsonSerializer.Serialize(timeline.StateAtAudioBeat(4, map)));
    }

    [Fact]
    public void WaveClockBridge_ExistingScoreRetainsMidiTiming()
    {
        var program = Parse("tempo 90 track music { C4 q E4 q G4 h }");
        var wave = TemporalAudioRenderer.BuildTempoMap(program);
        var midi = Interpreter.Interpret(program).TempoMap;
        foreach (var beat in new double[] { 0, 1, 4, 100 })
            Assert.Equal(midi.BeatsToMilliseconds(0, beat), wave.BeatsToMilliseconds(0, beat), 6);
        Assert.NotEmpty(Interpreter.Interpret(Parse("track set { C4 q } ")).Tracks);
    }

    [Fact]
    public void PcmOnsetFittingAndMultipleRails_RemainDeterministic()
    {
        var program = Parse("tempo 120 track cue { rest:4 C4 q } visual \"onset\" for 2s at 2s");
        var wav = TemporalAudioRenderer.RenderToWavBytes(program, TimeSpan.FromSeconds(4));
        using var stream = new MemoryStream(wav);
        var samples = WavReader.ReadMono(stream);
        Assert.All(samples.Take(2 * WavWriter.SampleRate), value => Assert.Equal(0f, value));
        Assert.Contains(samples.Skip(2 * WavWriter.SampleRate).Take(WavWriter.SampleRate / 2), value => Math.Abs(value) > 0.001f);
        Assert.All(samples.Skip(3 * WavWriter.SampleRate), value => Assert.Equal(0f, value));
        var mixed = AstToNoteEventAdapter.Adapt(Parse(AudioVisualUseCaseTests.Source("mixed-audio")));
        Assert.Equal(0, mixed.Tracks["cues"][0].StartTimeSeconds);
        Assert.Equal(2, mixed.Tracks["guide"][0].StartTimeSeconds);
        Assert.Equal(4, mixed.Tracks["speech"][0].StartTimeSeconds);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(30)]
    [InlineData(60)]
    public void ExportFps_DoesNotChangeMidpointGeometry(int fps)
    {
        var timeline = VisualInterpreter.Interpret(Parse(AudioVisualUseCaseTests.Source("progress-dashboard")));
        var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline, new(fps)));
        Assert.Equal(JsonSerializer.Serialize(TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(2.5)))),
            JsonSerializer.Serialize(plan.Samples[(int)(2.5 * fps)]));
    }

    [Fact]
    public void ProgressBars_HoldTheirLeftEdgeAndReachFullWidthBeforeTheEnd()
    {
        var timeline = VisualInterpreter.Interpret(Parse(AudioVisualUseCaseTests.Source("progress-dashboard")));
        foreach (var second in new[] { 4.0, 4.75, 5.5, 5.9 })
        {
            var fill = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(second)))
                .Primitives.Single(p => p.Name == "fill-2");
            Assert.Equal(445m, fill.Left);
            if (second >= 5.5) Assert.Equal(650m, fill.Width);
        }
    }
}
