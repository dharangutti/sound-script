// UNDER DEVELOPMENT — v2
// Verification for SoundScript.Wave v2: wavetable/fixed-point oscillator
// (determinism + A/B against the legacy trig path), band-limited waveforms
// (spectral regression), and the stereo rail (writer, constant-power panning,
// determinism). WaveRenderingTests (v1) is intentionally untouched — its
// passing unmodified proves the mono path survived v2.
using System.Text;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Io;
using SoundScript.Wave.Mixing;
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveV2Tests
{
    private const int SampleRate = 44_100;

    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    // ---- Determinism on the new default (wavetable) engine ----

    [Fact]
    public void WavetablePath_RenderToBytes_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            track pad {
                tempo 90 → 130 over 2 bars
                Cmaj q
                Dm q
                G7 h
            }
            """;

        // The default engine is the v2 wavetable path — no flag flipping needed.
        Assert.False(WaveEngineOptions.UseLegacyTrigOscillator);

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeterministicMath_MatchesFrameworkTrigWithinTolerance()
    {
        // Sanity: the Taylor-series sin/cos must agree with libm to far
        // better than audio precision across the argument range the
        // wavetable builder uses (loose 1e-9 tolerance leaves room for
        // platform libm variance, which is the whole reason the class exists).
        for (var i = -2000; i <= 2000; i++)
        {
            var x = i * 0.01;
            Assert.True(Math.Abs(DeterministicMath.Sin(x) - Math.Sin(x)) < 1e-9, $"sin({x})");
            Assert.True(Math.Abs(DeterministicMath.Cos(x) - Math.Cos(x)) < 1e-9, $"cos({x})");
        }
    }

    // ---- A/B: legacy trig path vs wavetable path ----

    [Fact]
    public void LegacyAndWavetablePaths_ProduceCloseButIndependentAudio()
    {
        var note = new NoteEvent(
            FrequencyHz: 440.0,
            StartTimeSeconds: 0.0,
            DurationSeconds: 0.25,
            Velocity: 0.8,
            Timbre: TimbreParams.Default);

        // Explicit-parameter internal overload instead of flipping the
        // global WaveEngineOptions flag: xUnit runs test classes in
        // parallel, and mutating shared static state here could race
        // against WaveRenderingTests' own renders. Same A/B, no leakage.
        var wavetable = NoteRenderer.Render(note, SampleRate, useLegacyTrigOscillator: false);
        var legacy = NoteRenderer.Render(note, SampleRate, useLegacyTrigOscillator: true);

        Assert.Equal(legacy.Length, wavetable.Length);

        // Both must be valid, non-degenerate audio of comparable shape —
        // not asserting bit-exactness between the two engines.
        Assert.InRange(Peak(wavetable), 0.4f, 0.8f);
        Assert.InRange(Peak(legacy), 0.4f, 0.8f);

        double dot = 0, wavetableEnergy = 0, legacyEnergy = 0;
        for (var i = 0; i < wavetable.Length; i++)
        {
            dot += (double)wavetable[i] * legacy[i];
            wavetableEnergy += (double)wavetable[i] * wavetable[i];
            legacyEnergy += (double)legacy[i] * legacy[i];
        }

        var correlation = dot / Math.Sqrt(wavetableEnergy * legacyEnergy);
        Assert.True(correlation > 0.999, $"Engine correlation too low: {correlation}");
    }

    [Fact]
    public void PublicRender_UsesWavetableEngineByDefault()
    {
        var note = new NoteEvent(220.0, 0.0, 0.1, 1.0, TimbreParams.Default);

        var viaPublicApi = NoteRenderer.Render(note, SampleRate);
        var viaExplicitWavetable = NoteRenderer.Render(note, SampleRate, useLegacyTrigOscillator: false);

        Assert.Equal(viaExplicitWavetable, viaPublicApi);
    }

    // ---- Spectral regression: band-limited Saw suppresses aliasing ----

    [Fact]
    public void BandLimitedSaw_SuppressesEnergyAboveHarmonicCutoff()
    {
        // 5000 Hz saw: band anchor 8192 allows floor(22050/8192) = 2
        // harmonics (5 kHz, 10 kHz). A naive saw would put its 3rd/4th/...
        // harmonics at 15 kHz, 20 kHz and alias the rest — all well above
        // the 11 kHz cutoff probed here.
        const double frequency = 5000.0;
        const double cutoffHz = 11_000.0;

        var note = new NoteEvent(
            FrequencyHz: frequency,
            StartTimeSeconds: 0.0,
            DurationSeconds: 0.3,
            Velocity: 1.0,
            Timbre: TimbreParams.Default with
            {
                Oscillator = OscillatorType.Saw,
                // Flat envelope: pure sustain, so the spectrum is clean.
                Envelope = new Adsr(Attack: 0.0, Decay: 0.0, Sustain: 1.0, Release: 0.0)
            });

        var bandLimited = NoteRenderer.Render(note, SampleRate, useLegacyTrigOscillator: false);
        var naive = NoteRenderer.Render(note, SampleRate, useLegacyTrigOscillator: true);

        var bandLimitedRatio = HighBandToFundamentalPowerRatio(bandLimited, frequency, cutoffHz);
        var naiveRatio = HighBandToFundamentalPowerRatio(naive, frequency, cutoffHz);

        Assert.True(bandLimitedRatio < 1e-3,
            $"Band-limited saw leaks above cutoff: ratio {bandLimitedRatio}");
        Assert.True(naiveRatio > 1e-2,
            $"Naive saw should alias well above cutoff (ratio {naiveRatio}) — is the legacy path still naive?");
    }

    // ---- Stereo: writer, header, panning, determinism ----

    [Fact]
    public void RenderStereoToBytes_ProducesValidStereoWavHeader()
    {
        const string source = """
            melody {
                bpm 100
                C4 q
                D4 q
                E4 h
            }
            """;

        var bytes = WaveRenderer.RenderStereoToBytes(ParseSsw(source));

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20));                     // PCM format tag
        Assert.Equal(2, BitConverter.ToInt16(bytes, 22));                     // stereo
        Assert.Equal(SampleRate, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(SampleRate * 2 * 16 / 8, BitConverter.ToInt32(bytes, 28)); // byte rate
        Assert.Equal(4, BitConverter.ToInt16(bytes, 32));                     // block align (2ch × 16-bit)
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));                    // bits per sample
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));

        var dataSize = BitConverter.ToInt32(bytes, 40);
        Assert.True(dataSize > 0);
        Assert.Equal(0, dataSize % 4); // whole interleaved L/R frames
        Assert.Equal(44 + dataSize, bytes.Length);

        // Same program, same notes: stereo data is exactly two channels'
        // worth of the mono render's frames.
        var monoBytes = WaveRenderer.RenderToBytes(ParseSsw(source));
        var monoDataSize = BitConverter.ToInt32(monoBytes, 40);
        Assert.Equal(monoDataSize * 2, dataSize);
    }

    [Fact]
    public void WriteStereo_RejectsMismatchedChannelLengths()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() =>
            WavWriter.WriteStereoTo(stream, new float[10], new float[9], SampleRate));
    }

    [Fact]
    public void WriteHeader_RejectsFrameCountThatOverflowsThe32BitDataSize()
    {
        // Regression: frameCount * blockAlign was computed with 32-bit int
        // arithmetic and written unchecked into the RIFF/data chunk sizes,
        // silently wrapping to a negative/garbage value for long enough
        // renders instead of failing loudly. WriteHeader takes frameCount as
        // a plain int (no array to allocate), so the overflow threshold can
        // be exercised directly without needing a multi-gigabyte buffer.
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Stereo 16-bit: blockAlign = 4 bytes/frame. int.MaxValue / 4 + 1
        // frames pushes frameCount * blockAlign just past Int32.MaxValue.
        var frameCount = int.MaxValue / 4 + 1;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WavWriter.WriteHeader(writer, channels: 2, sampleRate: SampleRate, frameCount));
        Assert.Contains("too long", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PanGains_AreConstantPowerAcrossTheWholeRange()
    {
        for (var i = -10; i <= 10; i++)
        {
            var pan = i / 10.0;
            var (left, right) = Mixer.PanGains(pan);

            Assert.Equal(1.0, left * left + right * right, 12);
        }

        var hardLeft = Mixer.PanGains(-1.0);
        Assert.Equal(1.0, hardLeft.Left, 12);
        Assert.Equal(0.0, hardLeft.Right, 12);

        var hardRight = Mixer.PanGains(1.0);
        Assert.Equal(0.0, hardRight.Left, 12);
        Assert.Equal(1.0, hardRight.Right, 12);

        var center = Mixer.PanGains(0.0);
        Assert.Equal(center.Left, center.Right, 12);
        Assert.Equal(Math.Sqrt(0.5), center.Left, 12);
    }

    [Fact]
    public void HardPannedNotes_LandOnlyInTheirChannel()
    {
        var hardLeftNote = new NoteEvent(
            440.0, 0.0, 0.1, 1.0, TimbreParams.Default with { Pan = -1.0 });

        var (left, right) = Mixer.RenderTrackStereo([hardLeftNote], SampleRate);

        Assert.True(Peak(left) > 0.1f, "Hard-left note produced no left-channel audio");
        Assert.All(right, s => Assert.Equal(0.0f, s)); // gain is exactly √0 = 0

        var hardRightNote = new NoteEvent(
            440.0, 0.0, 0.1, 1.0, TimbreParams.Default with { Pan = 1.0 });

        (left, right) = Mixer.RenderTrackStereo([hardRightNote], SampleRate);

        Assert.True(Peak(right) > 0.1f, "Hard-right note produced no right-channel audio");
        Assert.All(left, s => Assert.Equal(0.0f, s));
    }

    [Fact]
    public void StereoPath_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            track pad {
                tempo 90 → 130 over 2 bars
                Cmaj q
                Dm q
                G7 h
            }
            """;

        var first = WaveRenderer.RenderStereoToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderStereoToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void StereoPath_WithNonZeroPans_IsByteIdenticalAcrossRuns()
    {
        // Non-zero Pan can't come from the grammar (deliberately — see
        // Mixer.RenderTrackStereo), so exercise the direct-API route.
        static byte[] RenderOnce()
        {
            var notes = new List<NoteEvent>
            {
                new(440.0, 0.0, 0.2, 0.8, TimbreParams.Default with { Pan = -0.5 }),
                new(660.0, 0.1, 0.2, 0.8, TimbreParams.Default with
                {
                    Pan = 0.7,
                    Oscillator = OscillatorType.Square
                }),
            };

            var track = Mixer.RenderTrackStereo(notes, SampleRate);
            var (left, right) = Mixer.MixTracksStereo(new[] { track });

            using var stream = new MemoryStream();
            WavWriter.WriteStereoTo(stream, left, right, SampleRate);
            return stream.ToArray();
        }

        Assert.Equal(RenderOnce(), RenderOnce());
    }

    // ---- Mono v1 surface still intact ----

    [Fact]
    public void MonoPath_StillRendersValidMonoWav()
    {
        const string source = """
            melody {
                C4 q
                D4 q
            }
            """;

        var bytes = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22)); // still mono
        Assert.Equal(SampleRate, BitConverter.ToInt32(bytes, 24));
        Assert.True(bytes.Length > 44);
    }

    // ---- Helpers ----

    private static float Peak(float[] samples)
    {
        var peak = 0.0f;
        foreach (var s in samples)
            peak = Math.Max(peak, Math.Abs(s));
        return peak;
    }

    /// <summary>
    /// Direct O(n²) DFT (test-only; framework trig is fine here — the test
    /// asserts energy ratios, not bit-exact bytes) over a Hann-windowed
    /// slice of the sustain, returning (energy above cutoffHz) / (energy at
    /// the fundamental).
    /// </summary>
    private static double HighBandToFundamentalPowerRatio(
        float[] samples, double fundamentalHz, double cutoffHz)
    {
        const int windowStart = 4096;
        const int windowLength = 4096;
        Assert.True(samples.Length >= windowStart + windowLength, "Render too short for the DFT window");

        var windowed = new double[windowLength];
        for (var n = 0; n < windowLength; n++)
        {
            var hann = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * n / (windowLength - 1));
            windowed[n] = samples[windowStart + n] * hann;
        }

        var binCount = windowLength / 2;
        var power = new double[binCount];
        for (var k = 0; k < binCount; k++)
        {
            double re = 0.0, im = 0.0;
            for (var n = 0; n < windowLength; n++)
            {
                var angle = -2.0 * Math.PI * k * n / windowLength;
                re += windowed[n] * Math.Cos(angle);
                im += windowed[n] * Math.Sin(angle);
            }

            power[k] = re * re + im * im;
        }

        var binHz = (double)SampleRate / windowLength;

        // Fundamental power: peak bin within ±2 bins of the expected one
        // (Hann spreads the tone across neighbors).
        var fundamentalBin = (int)Math.Round(fundamentalHz / binHz);
        var fundamentalPower = 0.0;
        for (var k = Math.Max(0, fundamentalBin - 2); k <= Math.Min(binCount - 1, fundamentalBin + 2); k++)
            fundamentalPower = Math.Max(fundamentalPower, power[k]);

        Assert.True(fundamentalPower > 0.0, "No energy at the fundamental — degenerate render");

        var highBandPower = 0.0;
        for (var k = (int)Math.Ceiling(cutoffHz / binHz); k < binCount; k++)
            highBandPower += power[k];

        return highBandPower / fundamentalPower;
    }
}
