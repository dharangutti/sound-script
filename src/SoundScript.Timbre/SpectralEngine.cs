namespace SoundScript.Timbre;

/// <summary>
/// Deterministic cycle-accurate spectral synthesizer (V4.1). Each 8 ms frame
/// is reconstructed from 3–10 pitch cycles: harmonics → formants → noise → PCM.
/// </summary>
public static class SpectralEngine
{
    /// <summary>Synthesizes PCM samples for an entire timeline.</summary>
    public static float[] Synthesize(TimbreTimeline timeline)
    {
        var samples = new float[timeline.SampleCount];
        var frameSampleCount = (int)Math.Round(timeline.SampleRate * timeline.FrameMs / 1000.0);
        var formantFilter = new FormantFilter();
        var phaseOffset = 0.0;
        long noiseSeed = 0;

        foreach (var frame in timeline.Frames)
        {
            var outputOffset = frame.FrameIndex * frameSampleCount;
            if (outputOffset >= samples.Length)
                break;

            var frameLength = Math.Min(frameSampleCount, samples.Length - outputOffset);
            var amplitude = VelocityToAmplitude(frame.Velocity);

            CycleStitcher.StitchFrame(
                frame,
                frameLength,
                timeline.SampleRate,
                amplitude,
                noiseSeed,
                formantFilter,
                samples,
                outputOffset,
                ref phaseOffset);

            noiseSeed += frame.CycleCount * 1024 + frame.FrameIndex;
        }

        Normalize(samples);
        return samples;
    }

    private static double VelocityToAmplitude(int velocity) =>
        Math.Pow(Math.Clamp(velocity, 1, 127) / 127.0, 1.4) * 0.35;

    private static void Normalize(float[] samples)
    {
        var peak = 0.0f;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        if (peak <= 1e-6f)
            return;

        var scale = 0.95f / peak;
        for (var i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }
}
