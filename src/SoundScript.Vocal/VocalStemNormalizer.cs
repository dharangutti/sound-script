namespace SoundScript.Vocal;

/// <summary>Peak-normalizes offline vocal stems so they are audible in mixes.</summary>
internal static class VocalStemNormalizer
{
    internal const float TargetPeak = 0.92f;

    internal static float[] Normalize(float[] samples, double outputGain = 1.0)
    {
        if (samples.Length == 0)
            return samples;

        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        if (peak <= 1e-8)
            return samples;

        var scale = TargetPeak / peak * outputGain;
        var normalized = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
            normalized[i] = (float)Math.Clamp(samples[i] * scale, -1.0, 1.0);

        return normalized;
    }

    internal static double Peak(float[] samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        return peak;
    }
}
