namespace SoundScript.Timbre;

/// <summary>
/// Applies vowel formant resonators to a cycle buffer. Stateful across cycles
/// within a frame so filter continuity is preserved.
/// </summary>
public sealed class FormantFilter
{
    private const double TwoPi = Math.PI * 2.0;

    private readonly ResonatorState _f1 = new();
    private readonly ResonatorState _f2 = new();
    private readonly ResonatorState _f3 = new();
    private double _nasalMemory;

    /// <summary>Filters each sample in-place through three formants and nasal pole.</summary>
    public void Apply(double[] cycle, TimbreProfile profile, int sampleRate)
    {
        for (var i = 0; i < cycle.Length; i++)
        {
            var source = cycle[i];
            var f1 = Resonate(_f1, source, profile.Formant1Hz, profile.Formant1BwHz, sampleRate);
            var f2 = Resonate(_f2, source, profile.Formant2Hz, profile.Formant2BwHz, sampleRate);
            var f3 = Resonate(_f3, source, profile.Formant3Hz, profile.Formant3BwHz, sampleRate);
            var voiced = 0.55 * f1 + 0.3 * f2 + 0.15 * f3;
            cycle[i] = ApplyNasal(voiced, profile.Nasal);
        }
    }

    /// <summary>Deterministic single-sample formant shaping (stateless tests).</summary>
    public static double ShapeSample(double input, TimbreProfile profile, int sampleRate)
    {
        var filter = new FormantFilter();
        var buffer = new[] { input };
        filter.Apply(buffer, profile, sampleRate);
        return buffer[0];
    }

    private double ApplyNasal(double input, double nasal)
    {
        if (nasal <= 0)
            return input;

        var anti = input - _nasalMemory * 0.35;
        _nasalMemory = input;
        return input * (1.0 - nasal) + anti * nasal;
    }

    private static double Resonate(
        ResonatorState state,
        double input,
        double frequencyHz,
        double bandwidthHz,
        int sampleRate)
    {
        var r = Math.Exp(-Math.PI * bandwidthHz / Math.Max(frequencyHz, 1.0));
        var theta = TwoPi * frequencyHz / sampleRate;
        var cosine = Math.Cos(theta);
        var output = input + 2.0 * r * cosine * state.Y1 - r * r * state.Y2;
        state.Y2 = state.Y1;
        state.Y1 = output;
        return output;
    }

    private sealed class ResonatorState
    {
        public double Y1 { get; set; }
        public double Y2 { get; set; }
    }
}
