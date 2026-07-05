namespace SoundScript.Timbre;

/// <summary>
/// Stitches pitch cycles into one frame of PCM samples. Each frame contains
/// 3–10 cycles depending on pitch, reconstructed cycle-by-cycle like a DAC.
/// </summary>
public static class CycleStitcher
{
    private const double CrossfadeMs = 1.5;

    /// <summary>Carries the tail of the previous cycle across calls for boundary crossfading (V4.1.1).</summary>
    public sealed class CrossfadeState
    {
        internal double[]? Tail;
    }

    /// <summary>
    /// Synthesizes one frame by generating and stitching individual pitch cycles. When
    /// <paramref name="crossfade"/> is supplied, a short equal-power crossfade is blended
    /// across each cycle boundary (and across frame boundaries, if the same state instance
    /// persists) to remove micro-clicks (V4.1.1).
    /// </summary>
    public static void StitchFrame(
        TimbreFramePlan frame,
        int frameSampleCount,
        int sampleRate,
        double amplitude,
        long noiseSeed,
        FormantFilter formantFilter,
        float[] output,
        int outputOffset,
        ref double phaseOffset,
        CrossfadeState? crossfade = null)
    {
        if (frame.CycleCount <= 0 || frameSampleCount <= 0)
            return;

        var samplesWritten = 0;
        var noteElapsedMs = frame.StartMs - frame.NoteStartMs;

        for (var cycle = 0; cycle < frame.CycleCount && samplesWritten < frameSampleCount; cycle++)
        {
            var remaining = frameSampleCount - samplesWritten;
            var cycleSamples = remaining / (frame.CycleCount - cycle);
            if (cycleSamples <= 0)
                cycleSamples = 1;

            var harmonics = CycleGenerator.Generate(
                frame.PitchHz,
                frame.Profile,
                cycleSamples,
                sampleRate,
                phaseOffset);

            phaseOffset += (double)cycleSamples / CycleGenerator.SamplesPerCycle(frame.PitchHz, sampleRate);
            if (phaseOffset >= 1.0)
                phaseOffset -= Math.Floor(phaseOffset);

            var formantCycleIndex = frame.FrameIndex * 997 + cycle;
            formantFilter.Apply(harmonics, frame.Profile, sampleRate, formantCycleIndex);
            NoiseInjector.Inject(harmonics, frame.Profile, noiseSeed, cycle, noteElapsedMs, sampleRate);
            TransientModel.Apply(harmonics, frame.Profile, noteElapsedMs, sampleRate);

            if (crossfade is not null)
                ApplyCrossfadeIn(harmonics, crossfade, sampleRate);

            var position = (frame.StartMs + cycle * frame.CycleLengthMs - frame.NoteStartMs)
                / Math.Max(frame.NoteDurationMs, 1.0);
            var envelope = TransientModel.NoteEnvelope(position, frame.Profile.Smoothness);

            for (var i = 0; i < cycleSamples && samplesWritten < frameSampleCount; i++)
            {
                output[outputOffset + samplesWritten] += (float)(amplitude * envelope * harmonics[i]);
                samplesWritten++;
            }

            if (crossfade is not null)
                SaveCrossfadeTail(harmonics, crossfade, sampleRate);

            noteElapsedMs += cycleSamples * 1000.0 / sampleRate;
        }
    }

    private static void ApplyCrossfadeIn(double[] cycle, CrossfadeState state, int sampleRate)
    {
        if (state.Tail is not { Length: > 0 } tail)
            return;

        var crossfadeSamples = Math.Min(Math.Min(tail.Length, cycle.Length / 2), CrossfadeSampleCount(sampleRate));
        for (var i = 0; i < crossfadeSamples; i++)
        {
            var t = (i + 1) / (double)(crossfadeSamples + 1);
            var fadeIn = Math.Sin(t * Math.PI / 2.0);
            var fadeOut = Math.Cos(t * Math.PI / 2.0);
            cycle[i] = cycle[i] * fadeIn + tail[tail.Length - crossfadeSamples + i] * fadeOut;
        }
    }

    private static void SaveCrossfadeTail(double[] cycle, CrossfadeState state, int sampleRate)
    {
        var tailSamples = Math.Min(cycle.Length, CrossfadeSampleCount(sampleRate));
        var tail = new double[tailSamples];
        Array.Copy(cycle, cycle.Length - tailSamples, tail, 0, tailSamples);
        state.Tail = tail;
    }

    private static int CrossfadeSampleCount(int sampleRate) =>
        Math.Max(1, (int)Math.Round(CrossfadeMs * sampleRate / 1000.0));
}
