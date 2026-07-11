using SoundScript.Timbre;
using SoundScript.Wave.Effects;
using SoundScript.Wave.Io;
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Vocal;

/// <summary>
/// Applies a <see cref="DspTransformPlan"/> to mono samples as a deterministic,
/// cross-platform reference render. It reuses the repo's existing DSP primitives —
/// <see cref="OnePoleFilter"/> for EQ, <see cref="VocalStemProcessor"/> for pitch
/// shift and time-stretch resampling, <see cref="DeterministicMath"/> for the
/// vibrato LFO, and <see cref="DeterministicRandom"/> for the breath/noise layer —
/// so identical (input, plan, seed) yields byte-identical output.
///
/// <para>This is a reference transform for validation, not a studio-grade voice
/// effect: pitch and time-stretch are sequential linear resamples, and formant
/// shift is approximated as a spectral tilt.</para>
/// </summary>
public static class DspTransformRenderer
{
    private const int NoiseSalt = 0x5D5;

    public static float[] Render(
        float[] input,
        DspTransformPlan plan,
        int seed = 0,
        int sampleRate = WavWriter.SampleRate) =>
        Render(input, plan, seed, sampleRate, initialVibratoPhase: 0.0, noiseIndexOffset: 0);

    /// <summary>
    /// Continuity-aware overload used by <see cref="ContinuousVocalRenderer"/>:
    /// <paramref name="initialVibratoPhase"/> seeds the vibrato LFO so its phase
    /// carries across word boundaries, and <paramref name="noiseIndexOffset"/>
    /// advances the deterministic noise stream so the breath/noise floor is
    /// continuous rather than restarting per word.
    /// </summary>
    public static float[] Render(
        float[] input,
        DspTransformPlan plan,
        int seed,
        int sampleRate,
        double initialVibratoPhase,
        int noiseIndexOffset)
    {
        if (input.Length == 0)
            return input;

        var buffer = new double[input.Length];
        for (var i = 0; i < input.Length; i++)
            buffer[i] = input[i];

        foreach (var band in plan.EqBands)
            buffer = ApplyEqBand(buffer, band, sampleRate);

        buffer = ApplyFormantTilt(buffer, plan.FormantShift, sampleRate);

        if (plan.Vibrato.IsActive)
            buffer = ApplyVibrato(buffer, plan.Vibrato, sampleRate, initialVibratoPhase);

        var samples = new float[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            samples[i] = (float)buffer[i];

        if (Math.Abs(plan.PitchSemitones) > 1e-6)
            samples = VocalStemProcessor.PitchShift(samples, plan.PitchSemitones);

        if (Math.Abs(plan.TimeStretch - 1.0) > 1e-6)
        {
            var targetLength = Math.Max(1, (int)Math.Round(input.Length * plan.TimeStretch));
            samples = VocalStemProcessor.ResampleToLength(samples, targetLength);
        }

        var gain = Math.Pow(10.0, plan.GainDb / 20.0);
        var noiseScale = plan.NoiseLayer * 0.08;
        for (var i = 0; i < samples.Length; i++)
        {
            var value = samples[i] * gain;
            if (noiseScale > 0)
                value += DeterministicRandom.Unit(seed, noiseIndexOffset + i, NoiseSalt) * noiseScale;

            samples[i] = (float)Math.Clamp(value, -1.0, 1.0);
        }

        return samples;
    }

    private static double[] ApplyEqBand(double[] x, EqBand band, int sampleRate)
    {
        var g = Math.Pow(10.0, band.GainDb / 20.0);
        var nyquist = sampleRate / 2.0 - 1.0;

        switch (band.Shelf)
        {
            case EqShelf.LowShelf:
            {
                var low = LowPass(x, Clamp(band.PivotHz, nyquist), sampleRate);
                var output = new double[x.Length];
                for (var i = 0; i < x.Length; i++)
                    output[i] = low[i] * g + (x[i] - low[i]);
                return output;
            }
            case EqShelf.HighShelf:
            {
                var low = LowPass(x, Clamp(band.PivotHz, nyquist), sampleRate);
                var output = new double[x.Length];
                for (var i = 0; i < x.Length; i++)
                    output[i] = low[i] + (x[i] - low[i]) * g;
                return output;
            }
            default: // Peak — bandpass via difference of two low-passes
            {
                var wide = LowPass(x, Clamp(band.PivotHz * 1.5, nyquist), sampleRate);
                var narrow = LowPass(x, Clamp(band.PivotHz / 1.5, nyquist), sampleRate);
                var output = new double[x.Length];
                for (var i = 0; i < x.Length; i++)
                    output[i] = x[i] + (wide[i] - narrow[i]) * (g - 1.0);
                return output;
            }
        }
    }

    /// <summary>Approximates a formant shift as a gentle spectral tilt.</summary>
    private static double[] ApplyFormantTilt(double[] x, double formantShift, int sampleRate)
    {
        if (Math.Abs(formantShift - 1.0) < 1e-6)
            return x;

        // >1 (formants up) brightens; <1 (formants down) darkens.
        var gainDb = 6.0 * Math.Log2(formantShift);
        var band = new EqBand(3000, gainDb, EqShelf.HighShelf);
        return ApplyEqBand(x, band, sampleRate);
    }

    private static double[] ApplyVibrato(double[] x, VibratoParams vibrato, int sampleRate, double initialPhase)
    {
        // Modulated fractional delay ≈ periodic pitch warble. Deterministic sine.
        var maxDelaySamples = vibrato.DepthSemitones * 0.004 * sampleRate;
        var output = new double[x.Length];
        var angularStep = 2.0 * Math.PI * vibrato.RateHz / sampleRate;

        for (var i = 0; i < x.Length; i++)
        {
            var lfo = 0.5 + 0.5 * DeterministicMath.Sin(initialPhase + angularStep * i);
            var position = i - maxDelaySamples * lfo;
            output[i] = SampleLinear(x, position);
        }

        return output;
    }

    /// <summary>Advances the vibrato LFO phase by <paramref name="sampleCount"/> samples.</summary>
    internal static double AdvanceVibratoPhase(double phase, VibratoParams vibrato, int sampleCount, int sampleRate)
    {
        if (!vibrato.IsActive)
            return phase;

        return phase + 2.0 * Math.PI * vibrato.RateHz * sampleCount / sampleRate;
    }

    private static double[] LowPass(double[] x, double cutoffHz, int sampleRate) =>
        OnePoleFilter.Process(x, new FilterSettings(FilterKind.LowPass, cutoffHz), sampleRate);

    private static double Clamp(double cutoffHz, double nyquist) => Math.Clamp(cutoffHz, 1.0, nyquist);

    private static double SampleLinear(double[] x, double position)
    {
        if (position <= 0)
            return x[0];
        if (position >= x.Length - 1)
            return x[^1];

        var index = (int)position;
        var frac = position - index;
        return x[index] * (1 - frac) + x[index + 1] * frac;
    }
}
