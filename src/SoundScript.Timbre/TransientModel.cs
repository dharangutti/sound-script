namespace SoundScript.Timbre;

/// <summary>
/// Shapes consonant attack transients within each cycle.
/// </summary>
public static class TransientModel
{
    /// <summary>Applies transient envelope to a cycle buffer in-place.</summary>
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

        for (var i = 0; i < cycle.Length; i++)
        {
            var globalSample = noteElapsedSamples + i;
            if (globalSample >= transientSamples)
                continue;

            var t = globalSample / (double)transientSamples;
            var attack = t * t;
            cycle[i] *= 0.15 + 0.85 * attack;
        }
    }

    /// <summary>Note-level ADSR envelope for the frame.</summary>
    public static double NoteEnvelope(double position, double smoothness)
    {
        position = Math.Clamp(position, 0, 1);
        var attack = 0.05 + smoothness * 0.15;
        var release = 0.1 + smoothness * 0.2;

        if (position < attack)
            return position / attack;

        if (position > 1.0 - release)
            return Math.Max(0, (1.0 - position) / release);

        return 1.0;
    }
}
