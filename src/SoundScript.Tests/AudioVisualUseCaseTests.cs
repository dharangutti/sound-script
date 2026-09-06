using System.Diagnostics;
using System.Text.Json;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using Xunit;
using Xunit.Abstractions;

namespace SoundScript.Tests;

public class AudioVisualUseCaseTests(ITestOutputHelper output)
{
    public static readonly string[] Keys = [
        "org-chart", "delivery-flow", "sequence-diagram", "architecture", "block-diagram",
        "release-timeline", "status-dashboard", "progress-dashboard", "information-cards",
        "comparison", "startup-explainer", "title-captions", "presentation", "workflow",
        "network", "step-by-step", "kpi", "education", "mixed-audio", "scale-study"
    ];
    public static IEnumerable<object[]> Cases => Keys.Select(key => new object[] { key });
    internal static string Source(string key) => File.ReadAllText(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../examples", $"visual-{key}.ssv")));
    internal static ProgramNode Parse(string source) => new SoundScript.Parser.Parser(new Tokenizer(source).Tokenize()).Parse();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Theory]
    [MemberData(nameof(Cases))]
    public void PracticalComposition_CompilesSamplesAndRenders(string key)
    {
        var program = Parse(Source(key));
        var timeline = VisualInterpreter.Interpret(program);
        Assert.Equal(TimeSpan.FromSeconds(6), timeline.Duration);
        Assert.Single(timeline.AudioSyncPoints);
        Assert.True(timeline.Visuals.Count >= 5);
        Assert.Empty(timeline.StateAt(timeline.Duration).Elements);
        var timer = Stopwatch.StartNew();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline, new(24)));
        var planMilliseconds = timer.Elapsed.TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(144, plan.Samples.Count);
        foreach (var second in new[] { 0, 1, 2, 4, 5 })
        {
            var scene = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(second)));
            Assert.Equal(JsonSerializer.Serialize(scene), JsonSerializer.Serialize(plan.Samples[second * 24]));
            Assert.Equal(JsonSerializer.Serialize(scene), JsonSerializer.Serialize(
                TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(second)))));
            Assert.All(scene.Primitives, primitive => {
                Assert.NotNull(primitive.Paths);
                Assert.InRange(primitive.Left, 0, 1280);
                Assert.InRange(primitive.Top, 0, 720);
                Assert.InRange(primitive.Left + primitive.Width, 0, 1280);
                Assert.InRange(primitive.Top + primitive.Height, 0, 720);
            });
        }
        var wav = TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration);
        Assert.Equal(wav, TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration));
        using var stream = new MemoryStream(wav);
        Assert.Equal(6 * WavWriter.SampleRate, WavReader.ReadMono(stream).Length);
        var ppm = TemporalVideoFrameRenderer.RenderPpm(plan.Samples[96], 640, 360);
        Assert.Equal(ppm, TemporalVideoFrameRenderer.RenderPpm(plan.Samples[96], 640, 360));
        output.WriteLine($"{key}: {timeline.Visuals.Count} intervals, plan {planMilliseconds:F1} ms, allocated {allocated / 1048576.0:F2} MiB");

        // Optional verification artifacts for the real browser/FFmpeg harness.
        // Normal regression runs write no files.
        if (Environment.GetEnvironmentVariable("SOUNDSCRIPT_AV_ARTIFACTS") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, key + ".json"), JsonSerializer.Serialize(plan, JsonOptions));
            File.WriteAllBytes(Path.Combine(directory, key + ".wav"), wav);
            File.WriteAllBytes(Path.Combine(directory, key + ".ppm"), ppm);
            File.WriteAllText(Path.Combine(directory, key + ".metrics.json"), JsonSerializer.Serialize(new {
                intervals = timeline.Visuals.Count, planMilliseconds, allocatedBytes = allocated
            }));
        }
    }

    [Theory]
    [InlineData("delivery-flow", "explanation1", 2)]
    [InlineData("architecture", "explanation2", 4)]
    [InlineData("startup-explainer", "explanation1", 2)]
    [InlineData("presentation", "explanation2", 4)]
    [InlineData("mixed-audio", "speech", 4)]
    public void NarrationAttempts_UseExistingWaveScheduling(string key, string track, double start)
    {
        var program = Parse(Source(key));
        var audio = AstToNoteEventAdapter.Adapt(program);
        Assert.NotEmpty(audio.Tracks[track]);
        Assert.Equal(start, audio.Tracks[track][0].StartTimeSeconds, 6);
        Assert.Contains(audio.SpeakTimings, timing => Math.Abs(timing.StartTimeSeconds - start) < 0.000001);
        Assert.All(audio.Tracks[track], note => Assert.True(note.StartTimeSeconds + note.DurationSeconds <= 6));
        Assert.NotEmpty(VisualInterpreter.Interpret(program).StateAt(TimeSpan.FromSeconds(start)).Elements);
    }
}
