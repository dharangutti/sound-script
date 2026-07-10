using SoundScript.Wave.Io;

namespace SoundScript.Vocal;

/// <summary>DSP transforms for corpus pronunciation clips and synthesized stems.</summary>
internal static class VocalStemProcessor
{
    internal static float[] ApplyTransform(
        float[] samples,
        double trimStartMs,
        double? trimEndMs,
        double gain,
        double pitchSemitones,
        int sampleRate = WavWriter.SampleRate)
    {
        var trimmed = Trim(samples, trimStartMs, trimEndMs, sampleRate);
        var processed = Math.Abs(pitchSemitones) > 1e-6
            ? PitchShift(trimmed, pitchSemitones)
            : trimmed;

        if (Math.Abs(gain - 1.0) > 1e-6)
        {
            for (var i = 0; i < processed.Length; i++)
                processed[i] = (float)(processed[i] * gain);
        }

        return processed;
    }

    internal static float[] Concat(IReadOnlyList<float[]> parts)
    {
        var total = parts.Sum(part => part.Length);
        if (total == 0)
            return [];

        var output = new float[total];
        var offset = 0;
        foreach (var part in parts)
        {
            Array.Copy(part, 0, output, offset, part.Length);
            offset += part.Length;
        }

        return output;
    }

    internal static float[] Silence(double seconds, int sampleRate = WavWriter.SampleRate)
    {
        var length = Math.Max(0, (int)Math.Round(seconds * sampleRate));
        return new float[length];
    }

    private static float[] Trim(float[] samples, double trimStartMs, double? trimEndMs, int sampleRate)
    {
        var start = (int)Math.Clamp(trimStartMs / 1000.0 * sampleRate, 0, samples.Length);
        var end = trimEndMs is null
            ? samples.Length
            : (int)Math.Clamp(trimEndMs.Value / 1000.0 * sampleRate, start, samples.Length);

        if (start == 0 && end == samples.Length)
            return samples;

        var length = Math.Max(0, end - start);
        var trimmed = new float[length];
        Array.Copy(samples, start, trimmed, 0, length);
        return trimmed;
    }

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

    /// <summary>Deterministic linear-interpolation resample to an explicit length (time-stretch).</summary>
    internal static float[] ResampleToLength(float[] samples, int targetLength)
    {
        if (targetLength <= 0)
            return [];
        if (samples.Length == 0 || samples.Length == targetLength)
            return samples.Length == targetLength ? samples : new float[targetLength];

        var output = new float[targetLength];
        var ratio = (double)samples.Length / targetLength;

        for (var i = 0; i < targetLength; i++)
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
}
