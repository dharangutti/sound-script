using System.Security.Cryptography;
using SoundScript.Timbre;
using SoundScript.Vocal;
using SoundScript.Vocal.Wordbank;
using SoundScript.Wave.Io;
using SoundScript.Wave.Synthesis;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

public class ContinuousVocalRendererTests
{
    private const int SampleRate = WavWriter.SampleRate;

    private static float[] Tone(double hz = 220.0, double seconds = 0.2)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(0.5 * DeterministicMath.Sin(2.0 * Math.PI * hz * i / SampleRate));
        return samples;
    }

    private static float[] Constant(float value, double seconds = 0.05)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        Array.Fill(samples, value);
        return samples;
    }

    private static RenderedWord Word(float[] pcm) => new(pcm, new DspTransformPlan(), "w");

    [Fact]
    public void Stitch_EqualPowerCrossfade_ReducesBoundaryDiscontinuity()
    {
        var words = new[] { Word(Constant(0.8f)), Word(Constant(-0.8f)) };

        var hard = ContinuousVocalRenderer.Stitch(words, new VocalEngineOptions { CrossfadeMs = 0 });
        var faded = ContinuousVocalRenderer.Stitch(words, new VocalEngineOptions { CrossfadeMs = 10 });

        Assert.True(MaxAdjacentDelta(faded) < MaxAdjacentDelta(hard),
            "Crossfade should reduce the sample jump at the word boundary.");
        Assert.True(faded.Length < hard.Length, "Crossfade overlaps, so total length shrinks.");
    }

    [Fact]
    public void Stitch_SingleWord_ReturnsItsPcm()
    {
        var pcm = Tone();
        var output = ContinuousVocalRenderer.Stitch([Word(pcm)], new VocalEngineOptions());
        Assert.Equal(pcm, output);
    }

    [Fact]
    public void RenderWords_PitchGlide_PullsTowardPreviousWord()
    {
        var rules = SoundCSSParser.ParsePronunciations(
            """
            "hi" { pitch: +2; }
            "lo" { pitch: +10; }
            """);
        var options = new VocalEngineOptions { Pronunciations = rules, PitchSmoothing = 0.15 };
        var stem = Tone();

        var rendered = ContinuousVocalRenderer.RenderWords(["hi", "lo"], [stem, (float[])stem.Clone()], options);

        var basePitch = AudioNormalizeOps.DetectBasePitchHz(stem, SampleRate);
        var meta = CanonicalVoiceMetadata.Default with { BasePitchHz = basePitch };
        var prev = rendered[0].Plan.PitchSemitones;
        var rawLo = SoundCssDspMapper.Map(rules["lo"], meta).PitchSemitones;
        var expected = rawLo + (prev - rawLo) * 0.15;

        Assert.Equal(expected, rendered[1].Plan.PitchSemitones, 4);
        Assert.NotEqual(rawLo, rendered[1].Plan.PitchSemitones, 4);
    }

    [Fact]
    public void RenderWords_FormantGlide_PullsTowardPreviousWord()
    {
        var rules = SoundCSSParser.ParsePronunciations(
            """
            "a" { gender: male; }
            "b" { gender: female; }
            """);
        var options = new VocalEngineOptions { Pronunciations = rules, FormantSmoothing = 0.2 };
        var stem = Tone();

        var rendered = ContinuousVocalRenderer.RenderWords(["a", "b"], [stem, (float[])stem.Clone()], options);

        var meta = CanonicalVoiceMetadata.Default with
        {
            BasePitchHz = AudioNormalizeOps.DetectBasePitchHz(stem, SampleRate),
        };
        var prev = rendered[0].Plan.FormantShift;
        var rawB = SoundCssDspMapper.Map(rules["b"], meta).FormantShift;
        var expected = rawB + (prev - rawB) * 0.2;

        Assert.Equal(expected, rendered[1].Plan.FormantShift, 5);
    }

    [Fact]
    public void AdvanceVibratoPhase_AccumulatesOnlyWhenActive()
    {
        var active = new VibratoParams(5.0, 0.3);
        var advanced = DspTransformRenderer.AdvanceVibratoPhase(1.0, active, SampleRate, SampleRate);
        Assert.Equal(1.0 + 2.0 * Math.PI * 5.0, advanced, 6); // one second → 5 full cycles

        var idle = DspTransformRenderer.AdvanceVibratoPhase(1.0, VibratoParams.None, SampleRate, SampleRate);
        Assert.Equal(1.0, idle, 9);
    }

    [Fact]
    public void Render_WithInitialVibratoPhase_DiffersFromZeroPhase()
    {
        var input = Tone();
        var plan = new DspTransformPlan { Vibrato = new VibratoParams(5.5, 0.5) };

        var atZero = DspTransformRenderer.Render(input, plan, seed: 1, SampleRate, 0.0, 0);
        var atPi = DspTransformRenderer.Render(input, plan, seed: 1, SampleRate, Math.PI, 0);

        Assert.NotEqual(Hash(atZero), Hash(atPi));
    }

    [Fact]
    public void Assemble_IsDeterministic()
    {
        var rules = SoundCSSParser.ParsePronunciations("\"a\" { style: sing; vibrato: strong; pitch: +4; }");
        var options = new VocalEngineOptions { Pronunciations = rules, Continuous = true };
        var stems = new[] { Tone(), Tone(196.0), Tone(247.0) };
        var words = new[] { "a", "b", "c" };

        var first = ContinuousVocalRenderer.Assemble(words, stems, options);
        var second = ContinuousVocalRenderer.Assemble(words, stems, options);

        Assert.Equal(Hash(first), Hash(second));
    }

    private static double MaxAdjacentDelta(float[] samples)
    {
        var max = 0.0;
        for (var i = 1; i < samples.Length; i++)
            max = Math.Max(max, Math.Abs(samples[i] - samples[i - 1]));
        return max;
    }

    private static string Hash(float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

[Collection("WordbankCatalog")]
public class ContinuousVocalEngineTests : IDisposable
{
    public ContinuousVocalEngineTests()
    {
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
        CorpusCatalog.Reset();
        CorpusCatalog.TryLoadEmbedded();
    }

    public void Dispose()
    {
        CorpusCatalog.Reset();
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
    }

    [Fact]
    public void WordbankEngine_Continuous_ChangesOutputAndIsReproducible()
    {
        using var dir = new TempDir();
        var hard = dir.File("hard.wav");
        var cont = dir.File("cont.wav");
        var cont2 = dir.File("cont2.wav");

        new WordbankVocalEngine().Synthesize("jingle bells", hard, new VocalEngineOptions { Locale = "en" });
        new WordbankVocalEngine().Synthesize("jingle bells", cont, new VocalEngineOptions { Locale = "en", Continuous = true });
        new WordbankVocalEngine().Synthesize("jingle bells", cont2, new VocalEngineOptions { Locale = "en", Continuous = true });

        Assert.NotEqual(Hash(hard), Hash(cont));
        Assert.Equal(Hash(cont), Hash(cont2));
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class TempDir : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ss-cont-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(_root);
        public string File(string name) => Path.Combine(_root, name);
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
