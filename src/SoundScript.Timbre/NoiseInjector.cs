namespace SoundScript.Timbre;

/// <summary>
/// Injects deterministic fricative and plosive noise layers per cycle.
/// </summary>
public static class NoiseInjector
{
    /// <summary>Adds noise layers to a cycle buffer in-place.</summary>
    public static void Inject(
        double[] cycle,
        TimbreProfile profile,
        long noiseSeed,
        int cycleIndex,
        double noteElapsedMs)
    {
        var fricative = profile.NoiseFricative;
        var plosive = profile.NoisePlosive;
        var general = profile.Noise * 0.5;

        if (fricative <= 0 && plosive <= 0 && general <= 0)
            return;

        var plosiveDecay = PlosiveWeight(noteElapsedMs, profile.BurstMs);

        for (var i = 0; i < cycle.Length; i++)
        {
            var index = noiseSeed + cycleIndex * 997 + i;
            var n = DeterministicNoise(index);
            cycle[i] += n * (fricative + general);
            cycle[i] += n * plosive * plosiveDecay;
        }
    }

    /// <summary>Deterministic hash noise in [-1, 1].</summary>
    public static double DeterministicNoise(long index)
    {
        var x = Math.Sin(index * 12.9898 + 78.233) * 43758.5453;
        return x - Math.Floor(x) * 2.0 - 1.0;
    }

    private static double PlosiveWeight(double noteElapsedMs, double burstMs)
    {
        if (burstMs <= 0)
            return 0;

        if (noteElapsedMs >= burstMs)
            return 0;

        var t = noteElapsedMs / burstMs;
        return (1.0 - t) * (1.0 - t);
    }
}
