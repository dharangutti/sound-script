namespace SoundScript.Timbre;

/// <summary>
/// Stitches pitch cycles into one frame of PCM samples. Each frame contains
/// 3–10 cycles depending on pitch, reconstructed cycle-by-cycle like a DAC.
/// </summary>
public static class CycleStitcher
{
    /// <summary>
    /// Synthesizes one frame by generating and stitching individual pitch cycles.
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
        ref double phaseOffset)
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

            formantFilter.Apply(harmonics, frame.Profile, sampleRate);
            NoiseInjector.Inject(harmonics, frame.Profile, noiseSeed, cycle, noteElapsedMs);
            TransientModel.Apply(harmonics, frame.Profile, noteElapsedMs, sampleRate);

            var position = (frame.StartMs + cycle * frame.CycleLengthMs - frame.NoteStartMs)
                / Math.Max(frame.NoteDurationMs, 1.0);
            var envelope = TransientModel.NoteEnvelope(position, frame.Profile.Smoothness);

            for (var i = 0; i < cycleSamples && samplesWritten < frameSampleCount; i++)
            {
                output[outputOffset + samplesWritten] += (float)(amplitude * envelope * harmonics[i]);
                samplesWritten++;
            }

            noteElapsedMs += cycleSamples * 1000.0 / sampleRate;
        }
    }
}
