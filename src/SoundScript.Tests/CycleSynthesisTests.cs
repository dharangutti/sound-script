using SoundScript.Compose;
using SoundScript.Midi;
using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

public class CycleGeneratorTests
{
    [Fact]
    public void Generate_FundamentalDominatesHarmonicSeries()
    {
        var profile = PhonemeTimbreMapper.Map("aa");
        var cycle = CycleGenerator.Generate(440, profile, 100, 44100);

        var peak = cycle.Max(Math.Abs);
        Assert.True(peak > 0.5);
        Assert.Equal(100, cycle.Length);
    }

    [Theory]
    [InlineData(440)]
    [InlineData(220)]
    public void CycleLengthMs_MatchesPitch(double pitchHz)
    {
        var expected = 1000.0 / pitchHz;
        Assert.Equal(expected, CycleGenerator.CycleLengthMs(pitchHz), 6);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var profile = PhonemeTimbreMapper.Map("ee");
        var a = CycleGenerator.Generate(330, profile, 64, 44100, 0.1);
        var b = CycleGenerator.Generate(330, profile, 64, 44100, 0.1);
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(440, 8, 3, 10)]
    [InlineData(110, 8, 3, 10)]
    public void CycleCountForFrame_ClampedToRange(double pitchHz, double frameMs, int min, int max)
    {
        var count = CyclePlanner.CycleCountForFrame(pitchHz, frameMs);
        Assert.InRange(count, min, max);
    }
}

public class FormantFilterTests
{
    [Fact]
    public void Apply_ChangesCycleShape()
    {
        var profile = PhonemeTimbreMapper.Map("aa");
        var raw = CycleGenerator.Generate(440, profile, 128, 44100);
        var filtered = (double[])raw.Clone();

        var filter = new FormantFilter();
        filter.Apply(filtered, profile, 44100);

        Assert.NotEqual(raw, filtered);
    }

    [Fact]
    public void Apply_IsDeterministic()
    {
        var profile = PhonemeTimbreMapper.Map("oo");
        var a = FormantFilter.ShapeSample(0.5, profile, 44100);
        var b = FormantFilter.ShapeSample(0.5, profile, 44100);
        Assert.Equal(a, b);
    }
}

public class NoiseInjectorTests
{
    [Fact]
    public void Inject_AddsNoiseToPlosiveProfile()
    {
        var profile = PhonemeTimbreMapper.Map("p");
        var cycle = CycleGenerator.Generate(200, profile, 64, 44100);
        var before = cycle.Sum(Math.Abs);

        NoiseInjector.Inject(cycle, profile, noiseSeed: 42, cycleIndex: 0, noteElapsedMs: 0);
        var after = cycle.Sum(Math.Abs);

        Assert.True(after > before);
    }

    [Fact]
    public void DeterministicNoise_IsStable()
    {
        Assert.Equal(
            NoiseInjector.DeterministicNoise(12345),
            NoiseInjector.DeterministicNoise(12345));
    }

    [Fact]
    public void DeterministicNoise_StaysWithinDocumentedRange()
    {
        // Regression: `x - Math.Floor(x) * 2.0 - 1.0` is `x - (Math.Floor(x) * 2.0) - 1.0`,
        // not `(x - Math.Floor(x)) * 2.0 - 1.0` — operator precedence left the result
        // dominated by -Math.Floor(x), which grows with the ~43758.5 scale constant, so
        // output could reach the tens of thousands instead of staying in [-1, 1]. That fed
        // both the formant micro-drift and the raw noise layers with occasional huge spikes,
        // producing the large, note-boundary-unrelated audio clicks found in the renderer.
        for (long i = 0; i < 5000; i++)
        {
            var value = NoiseInjector.DeterministicNoise(i);
            Assert.InRange(value, -1.0, 1.0);
        }
    }
}

public class CycleStitcherTests
{
    [Fact]
    public void StitchFrame_WritesExpectedSampleCount()
    {
        var profile = PhonemeTimbreMapper.Map("aa");
        var frame = new TimbreFramePlan(
            0, 0, 0, 200, 440, 64, "aa", profile,
            CyclePlanner.CycleCountForFrame(440, 8),
            CycleGenerator.CycleLengthMs(440));

        var output = new float[352];
        var filter = new FormantFilter();
        var phase = 0.0;

        CycleStitcher.StitchFrame(frame, 352, 44100, 0.3, 0, filter, output, 0, ref phase);

        Assert.Contains(output, s => Math.Abs(s) > 0.001f);
    }

    [Fact]
    public void StitchFrame_IsDeterministic()
    {
        var profile = PhonemeTimbreMapper.Map("aa");
        var frame = new TimbreFramePlan(
            0, 0, 0, 200, 440, 64, "aa", profile, 4, CycleGenerator.CycleLengthMs(440));

        var a = StitchOnce(frame);
        var b = StitchOnce(frame);
        Assert.Equal(a, b);
    }

    private static float[] StitchOnce(TimbreFramePlan frame)
    {
        var output = new float[352];
        var filter = new FormantFilter();
        var phase = 0.0;
        CycleStitcher.StitchFrame(frame, 352, 44100, 0.3, 0, filter, output, 0, ref phase);
        return output;
    }
}

public class FullRenderTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void RenderSha256_IsStableAcrossRuns()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram(Example), stream);
        var midiBytes = stream.ToArray();
        var options = new OfflineRenderer.RenderOptions { SourceText = Example };

        var hashA = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);
        var hashB = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void Timeline_IncludesCycleFramePlans()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram("la"), stream);
        var temp = Path.Combine(Path.GetTempPath(), $"ss-v41-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, stream.ToArray());

        try
        {
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: PhonemeTimbreMapper.PhonemesFromText("la"),
                preferredTrackName: null);

            Assert.NotEmpty(timeline.Frames);
            Assert.All(timeline.Frames, f =>
            {
                Assert.InRange(f.CycleCount, CyclePlanner.MinCyclesPerFrame, CyclePlanner.MaxCyclesPerFrame);
                Assert.True(f.PitchHz > 0);
            });
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
