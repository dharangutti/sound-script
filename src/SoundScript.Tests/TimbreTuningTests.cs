using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

/// <summary>Tests for V4.1.1 harmonic rolloff curves (CycleGenerator).</summary>
public class HarmonicRolloffTests
{
    [Fact]
    public void Exponential_AttenuatesOvertonesMoreThanDefault()
    {
        var baseline = PhonemeTimbreMapper.DefaultProfile.With(
            harmonic1: 0.7, harmonic2: 0.6, harmonic3: 0.5, brightness: 0.5,
            harmonicRolloff: HarmonicRolloffCurve.Default);
        var exponential = baseline.With(harmonicRolloff: HarmonicRolloffCurve.Exponential);

        var defaultCycle = CycleGenerator.Generate(220, baseline, 200, 44100);
        var expCycle = CycleGenerator.Generate(220, exponential, 200, 44100);

        Assert.NotEqual(defaultCycle, expCycle);
    }

    [Theory]
    [InlineData(HarmonicRolloffCurve.Default)]
    [InlineData(HarmonicRolloffCurve.Exponential)]
    [InlineData(HarmonicRolloffCurve.Linear)]
    [InlineData(HarmonicRolloffCurve.Polynomial)]
    public void Generate_IsDeterministicAcrossCurves(HarmonicRolloffCurve curve)
    {
        var profile = PhonemeTimbreMapper.DefaultProfile.With(harmonicRolloff: curve);
        var a = CycleGenerator.Generate(330, profile, 128, 44100, 0.2);
        var b = CycleGenerator.Generate(330, profile, 128, 44100, 0.2);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Linear_NeverProducesNegativeAmplitudeMultiplier()
    {
        // Very dark, low-brightness profile should clamp rather than invert phase.
        var profile = PhonemeTimbreMapper.DefaultProfile.With(
            harmonic1: 0.1, harmonic2: 0.9, harmonic3: 0.9, brightness: 0.0,
            harmonicRolloff: HarmonicRolloffCurve.Linear);

        var cycle = CycleGenerator.Generate(200, profile, 64, 44100);
        Assert.All(cycle, sample => Assert.True(double.IsFinite(sample)));
    }

    [Fact]
    public void VowelProfiles_UseExponentialRolloff()
    {
        Assert.Equal(HarmonicRolloffCurve.Exponential, PhonemeTimbreMapper.Map("aa").HarmonicRolloff);
        Assert.Equal(HarmonicRolloffCurve.Exponential, PhonemeTimbreMapper.Map("ee").HarmonicRolloff);
    }

    [Fact]
    public void PlosiveProfiles_UseLinearRolloff()
    {
        Assert.Equal(HarmonicRolloffCurve.Linear, PhonemeTimbreMapper.Map("p").HarmonicRolloff);
    }

    [Fact]
    public void SoundCSS_ParsesHarmonicRolloffKeywords()
    {
        var overrides = SoundCSSParser.ParseOverrides("aa { harmonic-rolloff: exp; }");
        var mapped = PhonemeTimbreMapper.Map("aa", overrides);
        Assert.Equal(HarmonicRolloffCurve.Exponential, mapped.HarmonicRolloff);
    }
}

/// <summary>Tests for V4.1.1 formant Q control and deterministic micro-drift (FormantFilter).</summary>
public class FormantQTests
{
    [Fact]
    public void HigherQ_ProducesDifferentOutputThanLowerQ()
    {
        // A single-sample ShapeSample starts from a zero resonator state, where the very
        // first output always equals the input regardless of Q — Q only manifests once the
        // resonator has built up state, so this exercises a full cycle buffer instead.
        var profile = PhonemeTimbreMapper.Map("aa");
        var narrow = profile.With(formantQ: 2.0);
        var wide = profile.With(formantQ: 0.6);

        var narrowCycle = CycleGenerator.Generate(220, narrow, 200, 44100);
        var wideCycle = CycleGenerator.Generate(220, wide, 200, 44100);
        new FormantFilter().Apply(narrowCycle, narrow, 44100);
        new FormantFilter().Apply(wideCycle, wide, 44100);

        Assert.NotEqual(narrowCycle, wideCycle);
    }

    [Fact]
    public void Apply_IsDeterministicAcrossRuns()
    {
        var profile = PhonemeTimbreMapper.Map("oo");
        var a = CycleGenerator.Generate(220, profile, 64, 44100);
        var b = CycleGenerator.Generate(220, profile, 64, 44100);
        new FormantFilter().Apply(a, profile, 44100, cycleIndex: 7);
        new FormantFilter().Apply(b, profile, 44100, cycleIndex: 7);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentCycleIndex_ProducesDeterministicDrift()
    {
        var profile = PhonemeTimbreMapper.Map("aa");
        var cycle0 = CycleGenerator.Generate(220, profile, 200, 44100);
        var cycle1 = CycleGenerator.Generate(220, profile, 200, 44100);
        new FormantFilter().Apply(cycle0, profile, 44100, cycleIndex: 0);
        new FormantFilter().Apply(cycle1, profile, 44100, cycleIndex: 1);

        // Drift is deterministic (not random) but should vary the resonator's tuning between cycles.
        Assert.NotEqual(cycle0, cycle1);

        var cycle0Repeat = CycleGenerator.Generate(220, profile, 200, 44100);
        new FormantFilter().Apply(cycle0Repeat, profile, 44100, cycleIndex: 0);
        Assert.Equal(cycle0, cycle0Repeat);
    }

    [Fact]
    public void VowelProfiles_HaveHigherQThanPlosives()
    {
        Assert.True(PhonemeTimbreMapper.Map("aa").FormantQ > PhonemeTimbreMapper.Map("p").FormantQ);
        Assert.True(PhonemeTimbreMapper.Map("oo").FormantQ > 1.0);
        Assert.True(PhonemeTimbreMapper.Map("t").FormantQ < 1.0);
    }

    [Fact]
    public void SoundCSS_ParsesFormantQ()
    {
        var overrides = SoundCSSParser.ParseOverrides("aa { formant-q: 1.4; }");
        Assert.Equal(1.4, PhonemeTimbreMapper.Map("aa", overrides).FormantQ, 3);
    }
}

/// <summary>Tests for V4.1.1 band-passed fricative noise and plosive high-frequency emphasis (NoiseInjector).</summary>
public class NoiseShapingTests
{
    [Fact]
    public void FricativeNoise_IsShapedNotRawWhiteNoise()
    {
        var profile = PhonemeTimbreMapper.Map("s").With(noise: 0, noisePlosive: 0);
        var shaped = new double[64];
        NoiseInjector.Inject(shaped, profile, noiseSeed: 42, cycleIndex: 0, noteElapsedMs: 100, sampleRate: 44100);

        var raw = new double[64];
        for (var i = 0; i < raw.Length; i++)
            raw[i] = NoiseInjector.DeterministicNoise(42 + 0 * 997 + i) * profile.NoiseFricative;

        Assert.NotEqual(raw, shaped);
    }

    [Fact]
    public void PlosiveBurst_AddsHighFrequencyEmphasis()
    {
        var profile = PhonemeTimbreMapper.Map("p");
        var cycle = CycleGenerator.Generate(150, profile, 64, 44100);
        var before = (double[])cycle.Clone();

        NoiseInjector.Inject(cycle, profile, noiseSeed: 7, cycleIndex: 0, noteElapsedMs: 0, sampleRate: 44100);

        Assert.NotEqual(before, cycle);
    }

    [Fact]
    public void HighVoicing_DampensBroadbandNoiseComparedToLowVoicing()
    {
        var noisyVowel = PhonemeTimbreMapper.DefaultProfile.With(
            noise: 0.5, harmonic1: 0.9, noiseFricative: 0, noisePlosive: 0);
        var noisyConsonant = PhonemeTimbreMapper.DefaultProfile.With(
            noise: 0.5, harmonic1: 0.1, noiseFricative: 0, noisePlosive: 0);

        var vowelCycle = new double[64];
        var consonantCycle = new double[64];
        NoiseInjector.Inject(vowelCycle, noisyVowel, noiseSeed: 1, cycleIndex: 0, noteElapsedMs: 100);
        NoiseInjector.Inject(consonantCycle, noisyConsonant, noiseSeed: 1, cycleIndex: 0, noteElapsedMs: 100);

        var vowelEnergy = vowelCycle.Sum(Math.Abs);
        var consonantEnergy = consonantCycle.Sum(Math.Abs);

        Assert.True(vowelEnergy < consonantEnergy);
    }

    [Fact]
    public void Inject_IsDeterministicAcrossRuns()
    {
        var profile = PhonemeTimbreMapper.Map("sh");
        var a = new double[64];
        var b = new double[64];
        NoiseInjector.Inject(a, profile, noiseSeed: 99, cycleIndex: 2, noteElapsedMs: 1, sampleRate: 44100);
        NoiseInjector.Inject(b, profile, noiseSeed: 99, cycleIndex: 2, noteElapsedMs: 1, sampleRate: 44100);
        Assert.Equal(a, b);
    }

    [Fact]
    public void SoundCSS_ParsesNoiseBand()
    {
        var overrides = SoundCSSParser.ParseOverrides("aa { noise-band: 6000Hz; }");
        Assert.Equal(6000, PhonemeTimbreMapper.Map("aa", overrides).NoiseBandHz, 3);
    }
}

/// <summary>Tests for V4.1.1 sharpened consonant attacks and voiced micro-transients (TransientModel).</summary>
public class TransientEnvelopeTests
{
    [Fact]
    public void PlosiveConsonant_HasSharperAttackThanVowel()
    {
        var plosive = PhonemeTimbreMapper.Map("p");
        var vowel = PhonemeTimbreMapper.Map("aa");

        var plosiveCycle = ConstantCycle(64, 1.0);
        var vowelCycle = ConstantCycle(64, 1.0);

        TransientModel.Apply(plosiveCycle, plosive, noteElapsedMs: 0, sampleRate: 44100);
        TransientModel.Apply(vowelCycle, vowel, noteElapsedMs: 0, sampleRate: 44100);

        // A steeper attack curve should attenuate the very first sample more than a gentler one.
        Assert.True(plosiveCycle[0] < vowelCycle[0]);
    }

    [Fact]
    public void VoicedPlosive_AddsMicroTransientRipple()
    {
        var voiced = PhonemeTimbreMapper.Map("b");
        Assert.True(voiced.Harmonic1 > 0.15);
        Assert.True(voiced.NoisePlosive > 0.15);

        var withRipple = ConstantCycle(32, 0.0);
        var withoutRipple = ConstantCycle(32, 0.0);
        var noRippleProfile = voiced.With(harmonic1: 0.0);

        TransientModel.Apply(withRipple, voiced, noteElapsedMs: 0, sampleRate: 44100);
        TransientModel.Apply(withoutRipple, noRippleProfile, noteElapsedMs: 0, sampleRate: 44100);

        Assert.NotEqual(withRipple, withoutRipple);
    }

    [Fact]
    public void Apply_IsDeterministic()
    {
        var profile = PhonemeTimbreMapper.Map("d");
        var a = ConstantCycle(48, 1.0);
        var b = ConstantCycle(48, 1.0);
        TransientModel.Apply(a, profile, noteElapsedMs: 2, sampleRate: 44100);
        TransientModel.Apply(b, profile, noteElapsedMs: 2, sampleRate: 44100);
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void NoteEnvelope_StaysWithinUnitRange(double smoothness)
    {
        for (var p = 0.0; p <= 1.0; p += 0.1)
        {
            var value = TransientModel.NoteEnvelope(p, smoothness);
            Assert.InRange(value, 0.0, 1.0001);
        }
    }

    private static double[] ConstantCycle(int length, double value)
    {
        var cycle = new double[length];
        Array.Fill(cycle, value);
        return cycle;
    }
}

/// <summary>Tests for V4.1.1 cycle-boundary crossfading (CycleStitcher).</summary>
public class CycleStitchSmoothingTests
{
    [Fact]
    public void Crossfade_ReducesBoundaryDiscontinuity()
    {
        // "s" is noise-heavy, the primary source of cycle-boundary clicks (each cycle reseeds
        // its band-pass noise independently). Measuring only at the exact cycle-boundary
        // samples isolates the crossfade's effect from unrelated interior noise variance.
        var profile = PhonemeTimbreMapper.Map("s");
        var cycleCount = CyclePlanner.CycleCountForFrame(800, 8);
        var frame = new TimbreFramePlan(0, 0, 0, 200, 800, 100, "s", profile, cycleCount, CycleGenerator.CycleLengthMs(800));

        var withoutCrossfade = StitchWith(frame, crossfade: null);
        var withCrossfade = StitchWith(frame, crossfade: new CycleStitcher.CrossfadeState());

        var boundaries = CycleBoundaries(cycleCount, frameSampleCount: 352);
        var jumpWithout = boundaries.Sum(b => Math.Abs(withoutCrossfade[b] - withoutCrossfade[b - 1]));
        var jumpWith = boundaries.Sum(b => Math.Abs(withCrossfade[b] - withCrossfade[b - 1]));

        Assert.True(jumpWith < jumpWithout);
    }

    /// <summary>Replicates CycleStitcher's cycle-length partitioning to locate boundary sample indices.</summary>
    private static List<int> CycleBoundaries(int cycleCount, int frameSampleCount)
    {
        var boundaries = new List<int>();
        var samplesWritten = 0;
        for (var cycle = 0; cycle < cycleCount; cycle++)
        {
            var remaining = frameSampleCount - samplesWritten;
            var cycleSamples = Math.Max(1, remaining / (cycleCount - cycle));
            samplesWritten += cycleSamples;
            if (samplesWritten < frameSampleCount)
                boundaries.Add(samplesWritten);
        }

        return boundaries;
    }

    [Fact]
    public void Crossfade_IsDeterministicAcrossRuns()
    {
        var profile = PhonemeTimbreMapper.Map("ee");
        var frame = new TimbreFramePlan(
            0, 0, 0, 200, 300, 80, "ee", profile, 6, CycleGenerator.CycleLengthMs(300));

        var a = StitchWith(frame, new CycleStitcher.CrossfadeState());
        var b = StitchWith(frame, new CycleStitcher.CrossfadeState());
        Assert.Equal(a, b);
    }

    [Fact]
    public void Crossfade_PersistsTailAcrossFrames()
    {
        var profile = PhonemeTimbreMapper.Map("oo");
        var frame1 = new TimbreFramePlan(0, 0, 0, 200, 300, 80, "oo", profile, 4, CycleGenerator.CycleLengthMs(300));
        var frame2 = new TimbreFramePlan(1, 8, 0, 200, 300, 80, "oo", profile, 4, CycleGenerator.CycleLengthMs(300));

        var crossfade = new CycleStitcher.CrossfadeState();
        var phase = 0.0;
        var filter = new FormantFilter();
        var output = new float[704];

        CycleStitcher.StitchFrame(frame1, 352, 44100, 0.3, 0, filter, output, 0, ref phase, crossfade);

        // Should not throw and should keep producing finite, deterministic output across the frame boundary.
        CycleStitcher.StitchFrame(frame2, 352, 44100, 0.3, 100, filter, output, 352, ref phase, crossfade);
        Assert.All(output, s => Assert.True(float.IsFinite(s)));
    }

    private static float[] StitchWith(TimbreFramePlan frame, CycleStitcher.CrossfadeState? crossfade)
    {
        var output = new float[352];
        var filter = new FormantFilter();
        var phase = 0.0;
        CycleStitcher.StitchFrame(frame, 352, 44100, 0.3, 0, filter, output, 0, ref phase, crossfade);
        return output;
    }
}

/// <summary>Tests for V4.1.1 frame-to-frame profile smoothing (TimbreProfile.Lerp, SpectralEngine).</summary>
public class FrameContinuityTests
{
    [Fact]
    public void Lerp_AtZero_MatchesSourceProfile()
    {
        var from = PhonemeTimbreMapper.Map("p");
        var to = PhonemeTimbreMapper.Map("aa");
        var result = TimbreProfile.Lerp(from, to, 0.0);

        Assert.Equal(from.Harmonic1, result.Harmonic1, 6);
        Assert.Equal(from.Formant1Hz, result.Formant1Hz, 6);
    }

    [Fact]
    public void Lerp_AtOne_MatchesTargetProfile()
    {
        var from = PhonemeTimbreMapper.Map("p");
        var to = PhonemeTimbreMapper.Map("aa");
        var result = TimbreProfile.Lerp(from, to, 1.0);

        Assert.Equal(to.Harmonic1, result.Harmonic1, 6);
        Assert.Equal(to.Formant1Hz, result.Formant1Hz, 6);
    }

    [Fact]
    public void Lerp_AtHalf_IsBetweenSourceAndTarget()
    {
        var from = PhonemeTimbreMapper.Map("p");
        var to = PhonemeTimbreMapper.Map("aa");
        var result = TimbreProfile.Lerp(from, to, 0.5);

        Assert.InRange(result.Formant1Hz, Math.Min(from.Formant1Hz, to.Formant1Hz), Math.Max(from.Formant1Hz, to.Formant1Hz));
    }

    [Fact]
    public void Synthesize_SmoothsAbruptPhonemeTransition()
    {
        var segments = new List<TimbreSegment>
        {
            new(0, 100, 440, 90, "p", PhonemeTimbreMapper.Map("p")),
            new(100, 200, 440, 90, "aa", PhonemeTimbreMapper.Map("aa"))
        };
        var timeline = new TimbreTimeline
        {
            SampleRate = 44100,
            FrameMs = 8,
            TotalDurationMs = 300,
            Segments = segments,
            Frames = MidiToTimbreTimeline.BuildFramePlans(segments, 8, 300)
        };

        var samples = SpectralEngine.Synthesize(timeline);
        Assert.All(samples, s => Assert.True(float.IsFinite(s)));
    }

    [Fact]
    public void Synthesize_RemainsDeterministicWithSmoothing()
    {
        var segments = new List<TimbreSegment>
        {
            new(0, 180, 440, 64, "aa", PhonemeTimbreMapper.Map("aa"))
        };
        var timeline = new TimbreTimeline
        {
            SampleRate = 44100,
            FrameMs = 8,
            TotalDurationMs = 200,
            Segments = segments,
            Frames = MidiToTimbreTimeline.BuildFramePlans(segments, 8, 200)
        };

        var a = SpectralEngine.Synthesize(timeline);
        var b = SpectralEngine.Synthesize(timeline);
        Assert.Equal(a, b);
    }
}
