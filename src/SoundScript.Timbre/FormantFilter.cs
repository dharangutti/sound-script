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
    private double _nasalMemory2;

    /// <summary>
    /// Filters each sample in-place through three formants and a nasal pole.
    /// <paramref name="cycleIndex"/> drives deterministic ±2–5 Hz micro-drift per
    /// cycle so held vowels don't sound perfectly static (V4.1.1).
    /// </summary>
    public void Apply(double[] cycle, TimbreProfile profile, int sampleRate, int cycleIndex = 0)
    {
        var q = Math.Clamp(profile.FormantQ, 0.3, 3.0);
        var drift1 = Drift(cycleIndex, 1);
        var drift2 = Drift(cycleIndex, 2);
        var drift3 = Drift(cycleIndex, 3);

        for (var i = 0; i < cycle.Length; i++)
        {
            var source = cycle[i];
            var f1 = Resonate(_f1, source, profile.Formant1Hz + drift1, profile.Formant1BwHz / q, sampleRate);
            var f2 = Resonate(_f2, source, profile.Formant2Hz + drift2, profile.Formant2BwHz / q, sampleRate);
            var f3 = Resonate(_f3, source, profile.Formant3Hz + drift3, profile.Formant3BwHz / q, sampleRate);
            var voiced = 0.55 * f1 + 0.3 * f2 + 0.15 * f3;
            cycle[i] = ApplyNasal(voiced, profile.Nasal);
        }
    }

    /// <summary>Deterministic single-sample formant shaping (stateless tests).</summary>
    public static double ShapeSample(double input, TimbreProfile profile, int sampleRate, int cycleIndex = 0)
    {
        var filter = new FormantFilter();
        var buffer = new[] { input };
        filter.Apply(buffer, profile, sampleRate, cycleIndex);
        return buffer[0];
    }

    /// <summary>Deterministic ±2–5 Hz per-cycle formant drift (hash-based, no RNG).</summary>
    private static double Drift(int cycleIndex, int formantNumber)
    {
        var noise = NoiseInjector.DeterministicNoise(cycleIndex * 131L + formantNumber * 977L);
        return noise * 3.5;
    }

    private double ApplyNasal(double input, double nasal)
    {
        if (nasal <= 0)
            return input;

        // Second-order anti-resonance for a sharper, more natural nasal notch.
        var anti = input - _nasalMemory * 0.35 + _nasalMemory2 * 0.12;
        _nasalMemory2 = _nasalMemory;
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
