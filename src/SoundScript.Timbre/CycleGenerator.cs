namespace SoundScript.Timbre;

/// <summary>
/// Generates one pitch period of harmonic content. Each cycle is a complete
/// waveform reconstruction unit — the fundamental plus overtone amplitudes
/// defined by the timbre profile.
/// </summary>
public static class CycleGenerator
{
    private const double TwoPi = Math.PI * 2.0;

    /// <summary>
    /// Synthesizes one cycle of harmonic samples at the given pitch.
    /// </summary>
    /// <param name="pitchHz">Fundamental frequency in Hz.</param>
    /// <param name="profile">Harmonic amplitudes and spectral tilt.</param>
    /// <param name="sampleCount">Number of PCM samples in this cycle.</param>
    /// <param name="sampleRate">Output sample rate.</param>
    /// <param name="phaseOffset">Continuous phase offset in cycles (0–1).</param>
    public static double[] Generate(
        double pitchHz,
        TimbreProfile profile,
        int sampleCount,
        int sampleRate,
        double phaseOffset = 0)
    {
        var output = new double[sampleCount];
        if (sampleCount <= 0 || pitchHz <= 0)
            return output;

        var tilt = 1.0 - profile.Brightness * 0.35;
        var h1 = profile.Harmonic1;
        var h2 = profile.Harmonic2 * Rolloff(profile.HarmonicRolloff, 2, profile.Brightness, tilt);
        var h3 = profile.Harmonic3 * Rolloff(profile.HarmonicRolloff, 3, profile.Brightness, tilt);

        for (var i = 0; i < sampleCount; i++)
        {
            var t = phaseOffset + i * pitchHz / sampleRate;
            var angle = TwoPi * t;
            var fundamental = Math.Sin(angle) * h1;
            var second = Math.Sin(angle * 2.0) * h2;
            var third = Math.Sin(angle * 3.0) * h3;
            output[i] = fundamental + second + third;
        }

        return output;
    }

    /// <summary>
    /// Amplitude multiplier for the given overtone above the fundamental, shaped by the
    /// phoneme's rolloff curve (V4.1.1). <see cref="HarmonicRolloffCurve.Default"/>
    /// reproduces the original V4.1 brightness-tilt behavior exactly.
    /// </summary>
    private static double Rolloff(HarmonicRolloffCurve curve, int harmonicNumber, double brightness, double legacyTilt) =>
        curve switch
        {
            HarmonicRolloffCurve.Exponential => Math.Exp(-(harmonicNumber - 1) * (1.3 - brightness * 0.6)),
            HarmonicRolloffCurve.Linear => Math.Clamp(1.0 - (harmonicNumber - 1) * (0.4 - brightness * 0.15), 0, 1),
            HarmonicRolloffCurve.Polynomial => 1.0 / Math.Pow(harmonicNumber, 1.1 + (1.0 - brightness) * 0.7),
            _ => Math.Pow(legacyTilt, harmonicNumber - 1)
        };

    /// <summary>Sample count for one pitch period at the given rate.</summary>
    public static int SamplesPerCycle(double pitchHz, int sampleRate) =>
        Math.Max(1, (int)Math.Round(sampleRate / Math.Max(pitchHz, 1.0)));

    /// <summary>Cycle duration in milliseconds.</summary>
    public static double CycleLengthMs(double pitchHz) =>
        pitchHz > 0 ? 1000.0 / pitchHz : 8.0;
}
