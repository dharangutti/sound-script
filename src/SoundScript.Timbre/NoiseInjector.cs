namespace SoundScript.Timbre;

/// <summary>
/// Injects deterministic fricative and plosive noise layers per cycle.
/// </summary>
public static class NoiseInjector
{
    private const double DefaultFricativeBandHz = 6000.0;
    private const double FricativeBandQ = 2.5;
    private const double PlosivePreEmphasis = 0.6;

    /// <summary>
    /// Adds noise layers to a cycle buffer in-place. Fricative noise is shaped through a
    /// deterministic band-pass around <see cref="TimbreProfile.NoiseBandHz"/> (4–8 kHz by
    /// default); plosive noise gets a high-frequency pre-emphasis burst; broadband noise is
    /// damped for strongly-voiced (vowel-like) profiles (V4.1.1).
    /// </summary>
    public static void Inject(
        double[] cycle,
        TimbreProfile profile,
        long noiseSeed,
        int cycleIndex,
        double noteElapsedMs,
        int sampleRate = 44100)
    {
        var fricative = profile.NoiseFricative;
        var plosive = profile.NoisePlosive;
        var voicingDamping = 1.0 - Math.Clamp(profile.Harmonic1, 0, 1) * 0.5;
        var general = profile.Noise * 0.5 * voicingDamping;

        if (fricative <= 0 && plosive <= 0 && general <= 0)
            return;

        var plosiveSharpness = Math.Clamp(profile.NoisePlosive, 0, 1);
        var plosiveDecay = PlosiveWeight(noteElapsedMs, profile.BurstMs, plosiveSharpness);

        var raw = new double[cycle.Length];
        for (var i = 0; i < cycle.Length; i++)
            raw[i] = DeterministicNoise(noiseSeed + cycleIndex * 997 + i);

        var bandHz = Math.Clamp(
            profile.NoiseBandHz > 0 ? profile.NoiseBandHz : DefaultFricativeBandHz,
            200.0,
            Math.Max(300.0, sampleRate / 2.0 - 100.0));

        var fricativeShaped = fricative > 0 ? BandPass(raw, bandHz, sampleRate, FricativeBandQ) : null;
        var plosiveShaped = plosive > 0 ? HighFrequencyEmphasis(raw) : null;

        for (var i = 0; i < cycle.Length; i++)
        {
            if (fricativeShaped is not null)
                cycle[i] += fricativeShaped[i] * fricative;
            if (general > 0)
                cycle[i] += raw[i] * general;
            if (plosiveShaped is not null)
                cycle[i] += plosiveShaped[i] * plosive * plosiveDecay;
        }
    }

    /// <summary>Deterministic hash noise in [-1, 1].</summary>
    public static double DeterministicNoise(long index)
    {
        var x = Math.Sin(index * 12.9898 + 78.233) * 43758.5453;
        return (x - Math.Floor(x)) * 2.0 - 1.0;
    }

    /// <summary>Deterministic resonant band-pass, shaping fricative white noise around a centre frequency.</summary>
    private static double[] BandPass(double[] input, double centerHz, int sampleRate, double q)
    {
        var bandwidthHz = Math.Max(centerHz / q, 1.0);
        var r = Math.Exp(-Math.PI * bandwidthHz / centerHz);
        var theta = 2.0 * Math.PI * centerHz / sampleRate;
        var cosine = Math.Cos(theta);
        var gain = 1.0 - r;

        var output = new double[input.Length];
        var y1 = 0.0;
        var y2 = 0.0;
        for (var i = 0; i < input.Length; i++)
        {
            var y = gain * input[i] + 2.0 * r * cosine * y1 - r * r * y2;
            y2 = y1;
            y1 = y;
            output[i] = y;
        }

        return output;
    }

    /// <summary>Simple deterministic differencing high-pass — emphasizes high frequencies in plosive bursts.</summary>
    private static double[] HighFrequencyEmphasis(double[] input)
    {
        var output = new double[input.Length];
        var previous = 0.0;
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] - previous * PlosivePreEmphasis;
            previous = input[i];
        }

        return output;
    }

    /// <summary>Phoneme-specific noise envelope: sharper plosives decay faster (V4.1.1).</summary>
    private static double PlosiveWeight(double noteElapsedMs, double burstMs, double sharpness)
    {
        if (burstMs <= 0)
            return 0;

        if (noteElapsedMs >= burstMs)
            return 0;

        var t = noteElapsedMs / burstMs;
        var exponent = 1.6 + sharpness;
        return Math.Pow(1.0 - t, exponent);
    }
}
