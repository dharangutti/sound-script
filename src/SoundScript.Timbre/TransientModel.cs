namespace SoundScript.Timbre;

/// <summary>
/// Shapes consonant attack transients within each cycle.
/// </summary>
public static class TransientModel
{
    private const double TwoPi = Math.PI * 2.0;

    /// <summary>
    /// Applies transient envelope to a cycle buffer in-place. Plosive-heavy profiles
    /// (p, t, k) get a steeper attack curve; voiced plosives (b, d, g — high
    /// <see cref="TimbreProfile.Harmonic1"/> alongside plosive noise) get a subtle
    /// deterministic micro-transient ripple to simulate the voice bar (V4.1.1).
    /// </summary>
    public static void Apply(
        double[] cycle,
        TimbreProfile profile,
        double noteElapsedMs,
        int sampleRate)
    {
        var transientMs = Math.Max(profile.TransientMs, profile.BurstMs * 0.5);
        if (transientMs <= 0)
            return;

        var transientSamples = (int)Math.Round(transientMs * sampleRate / 1000.0);
        if (transientSamples <= 0)
            return;

        var noteElapsedSamples = (int)Math.Round(noteElapsedMs * sampleRate / 1000.0);
        var plosiveness = Math.Clamp(profile.NoisePlosive, 0, 1);
        var sharpness = 2.0 + plosiveness * 3.0;
        var floor = 0.15 - plosiveness * 0.08;
        var voicedMicroTransient = profile.Harmonic1 > 0.15 && plosiveness > 0.15;
        var pulsePeriod = Math.Max(1.0, transientSamples / 3.0);

        for (var i = 0; i < cycle.Length; i++)
        {
            var globalSample = noteElapsedSamples + i;
            if (globalSample >= transientSamples)
                continue;

            var t = globalSample / (double)transientSamples;
            var attack = Math.Pow(t, sharpness);
            cycle[i] *= floor + (1.0 - floor) * attack;

            if (voicedMicroTransient)
                cycle[i] += Math.Sin(TwoPi * globalSample / pulsePeriod) * 0.05 * profile.Harmonic1 * (1.0 - t);
        }
    }

    /// <summary>Note-level ADSR envelope for the frame, shaped by <paramref name="smoothness"/>.</summary>
    public static double NoteEnvelope(double position, double smoothness)
    {
        position = Math.Clamp(position, 0, 1);
        smoothness = Math.Clamp(smoothness, 0, 1);
        var attack = 0.05 + smoothness * 0.15;
        var release = 0.1 + smoothness * 0.2;

        if (position < attack)
            return Math.Pow(position / attack, 1.0 + smoothness);

        if (position > 1.0 - release)
        {
            var t = Math.Max(0, (1.0 - position) / release);
            return Math.Pow(t, 1.0 + (1.0 - smoothness) * 0.5);
        }

        return 1.0;
    }
}
