// UNDER DEVELOPMENT — v3
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Effects;

/// <summary>
/// Simple feedback delay line. Fully deterministic: the delay is
/// sample-indexed (an integer number of samples, never wall-clock), state is
/// a plain circular buffer iterated in sample order, and only IEEE-exact
/// <c>+ - * /</c> arithmetic is involved — same input buffer + same settings
/// = same output buffer, always.
///
/// The output is extended past the input by a decaying echo tail (computed
/// deterministically from feedback/mix, capped) rather than truncating echoes
/// mid-ring at the original buffer length.
/// </summary>
internal static class DelayEffect
{
    // Echoes below this amplitude are considered inaudible for tail sizing
    // (well under one 16-bit LSB, 1/32768 ≈ 3.1e-5, after normalization).
    private const double TailFloor = 1e-4;
    private const int MaxTailRepeats = 64;

    internal static double[] Process(double[] input, DelaySettings settings, int sampleRate)
    {
        var delaySamples = Math.Max(1, (int)Math.Round(settings.TimeSeconds * sampleRate));
        var output = new double[input.Length + delaySamples * TailRepeats(settings)];
        var line = new double[delaySamples];
        var index = 0;

        for (var i = 0; i < output.Length; i++)
        {
            var dry = i < input.Length ? input[i] : 0.0;
            var delayed = line[index];

            output[i] = dry * (1.0 - settings.Mix) + delayed * settings.Mix;
            line[index] = dry + delayed * settings.Feedback;

            index++;
            if (index == delaySamples)
                index = 0;
        }

        return output;
    }

    /// <summary>
    /// Number of delay periods to append: enough for the wet signal to decay
    /// below <see cref="TailFloor"/> (echo n has amplitude mix·feedback^(n-1)),
    /// capped so pathological feedback values can't produce unbounded output.
    /// </summary>
    private static int TailRepeats(DelaySettings settings)
    {
        if (settings.Mix <= 0.0)
            return 0; // fully dry — no audible tail to preserve

        var amplitude = settings.Mix;
        var repeats = 1;
        while (repeats < MaxTailRepeats && amplitude * settings.Feedback >= TailFloor)
        {
            amplitude *= settings.Feedback;
            repeats++;
        }

        return repeats;
    }
}
