namespace SoundScript.Vocal.Wordbank;

/// <summary>
/// Pure, deterministic DSP primitives used by <see cref="WordbankNormalizer"/>:
/// silence trimming, peak/RMS gain, naive pitch shift, RMS, and autocorrelation
/// pitch detection. No randomness and no platform-dependent state, so identical
/// inputs always yield identical outputs.
/// </summary>
internal static class AudioNormalizeOps
{
    /// <summary>
    /// Trims leading/trailing samples whose magnitude stays below
    /// <paramref name="threshold"/>, keeping <paramref name="paddingMs"/> of
    /// context on each side. Returns the input unchanged when it is entirely
    /// silent or already tight.
    /// </summary>
    internal static float[] TrimSilence(float[] samples, double threshold, double paddingMs, int sampleRate)
    {
        if (samples.Length == 0)
            return samples;

        var first = -1;
        var last = -1;
        for (var i = 0; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i]) >= threshold)
            {
                if (first < 0)
                    first = i;
                last = i;
            }
        }

        if (first < 0)
            return samples; // fully silent — leave as-is for a stable, non-empty result

        var pad = Math.Max(0, (int)Math.Round(paddingMs / 1000.0 * sampleRate));
        var start = Math.Max(0, first - pad);
        var end = Math.Min(samples.Length - 1, last + pad);

        var length = end - start + 1;
        if (start == 0 && length == samples.Length)
            return samples;

        var trimmed = new float[length];
        Array.Copy(samples, start, trimmed, 0, length);
        return trimmed;
    }

    /// <summary>
    /// Scales the signal toward <paramref name="targetRms"/> while never letting
    /// the peak exceed <paramref name="targetPeak"/> (the smaller of the two
    /// gains wins). Silent buffers are returned untouched.
    /// </summary>
    internal static float[] NormalizeGain(float[] samples, double targetPeak, double targetRms, double silenceFloor)
    {
        if (samples.Length == 0)
            return samples;

        var peak = Peak(samples);
        if (peak <= silenceFloor)
            return samples;

        var rms = Rms(samples);
        var peakGain = targetPeak / peak;
        var rmsGain = rms > silenceFloor ? targetRms / rms : peakGain;
        var gain = Math.Min(peakGain, rmsGain);

        var output = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
            output[i] = (float)Math.Clamp(samples[i] * gain, -1.0, 1.0);

        return output;
    }

    /// <summary>Naive resampling pitch shift (positive semitones raise pitch).</summary>
    internal static float[] PitchShift(float[] samples, double semitones)
    {
        if (samples.Length == 0)
            return samples;

        var ratio = Math.Pow(2.0, semitones / 12.0);
        var outputLength = Math.Max(1, (int)Math.Round(samples.Length / ratio));
        var output = new float[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var source = i * ratio;
            var index = (int)source;
            var frac = source - index;
            if (index >= samples.Length - 1)
            {
                output[i] = samples[^1];
                continue;
            }

            output[i] = (float)(samples[index] * (1 - frac) + samples[index + 1] * frac);
        }

        return output;
    }

    internal static double Peak(float[] samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));
        return peak;
    }

    internal static double Rms(float[] samples)
    {
        if (samples.Length == 0)
            return 0.0;

        var sum = 0.0;
        foreach (var sample in samples)
            sum += (double)sample * sample;

        return Math.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// Estimates the fundamental frequency (Hz) via normalized autocorrelation
    /// over the 70–400 Hz voiced band, with parabolic interpolation for a stable
    /// sub-sample estimate. Returns 0 when the signal is too short or unvoiced.
    /// </summary>
    internal static double DetectBasePitchHz(float[] samples, int sampleRate)
    {
        const double minHz = 70.0;
        const double maxHz = 400.0;
        const double voicingThreshold = 0.3;
        const int maxAnalysisSamples = 44_100; // cap to ~1s for stable, bounded cost

        if (samples.Length == 0)
            return 0.0;

        var minLag = (int)Math.Floor(sampleRate / maxHz);
        var maxLag = (int)Math.Ceiling(sampleRate / minHz);
        var count = Math.Min(samples.Length, maxAnalysisSamples);
        if (count <= maxLag + 1)
            return 0.0;

        var energy = 0.0;
        for (var i = 0; i < count; i++)
            energy += (double)samples[i] * samples[i];

        if (energy <= 1e-9)
            return 0.0;

        var bestLag = -1;
        var bestCorr = double.NegativeInfinity;

        // Track correlations to allow parabolic interpolation around the peak.
        var corr = new double[maxLag + 2];
        for (var lag = minLag; lag <= maxLag; lag++)
        {
            var sum = 0.0;
            for (var i = 0; i < count - lag; i++)
                sum += (double)samples[i] * samples[i + lag];

            var normalized = sum / energy;
            corr[lag] = normalized;
            if (normalized > bestCorr)
            {
                bestCorr = normalized;
                bestLag = lag;
            }
        }

        if (bestLag < 0 || bestCorr < voicingThreshold)
            return 0.0;

        var corrBestMinus1 = bestLag - 1 >= minLag ? corr[bestLag - 1] : corr[bestLag];
        var corrBestPlus1 = bestLag + 1 <= maxLag ? corr[bestLag + 1] : corr[bestLag];

        var refinedLag = ParabolicPeak(corrBestMinus1, corr[bestLag], corrBestPlus1, bestLag);
        if (refinedLag <= 0)
            return 0.0;

        return sampleRate / refinedLag;
    }

    private static double ParabolicPeak(double yMinus, double yCenter, double yPlus, int centerLag)
    {
        var denom = yMinus - 2 * yCenter + yPlus;
        if (Math.Abs(denom) < 1e-12)
            return centerLag;

        var offset = 0.5 * (yMinus - yPlus) / denom;
        // Guard against runaway offsets from a flat/near-degenerate peak.
        if (offset is < -1.0 or > 1.0)
            return centerLag;

        return centerLag + offset;
    }
}
