// UNDER DEVELOPMENT — v3
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Effects;

/// <summary>
/// Single-pole IIR low-pass / high-pass filter (6 dB per octave — the v3
/// scope; steeper slopes are a parking-lot follow-up).
///
/// Determinism note: the coefficient uses the RC discretization
/// α = w/(1+w) with w = 2π·fc/fs instead of the textbook 1−exp(−w) form,
/// because Math.Exp is a transcendental whose last bits vary across platform
/// libms, while this form needs only the compile-time constant Math.PI and
/// IEEE-exact <c>* /</c> — the same reasoning as DeterministicMath and the
/// sqrt-law pan gains. The per-sample recurrence is plain <c>+ - *</c>.
/// </summary>
internal static class OnePoleFilter
{
    internal static double[] Process(double[] input, FilterSettings settings, int sampleRate)
    {
        var w = 2.0 * Math.PI * settings.CutoffHz / sampleRate;
        var output = new double[input.Length];

        if (settings.Kind == FilterKind.LowPass)
        {
            // y[n] = y[n-1] + α (x[n] − y[n-1])
            var alpha = w / (1.0 + w);
            var y = 0.0;
            for (var i = 0; i < input.Length; i++)
            {
                y += alpha * (input[i] - y);
                output[i] = y;
            }
        }
        else
        {
            // y[n] = a (y[n-1] + x[n] − x[n-1])
            var a = 1.0 / (1.0 + w);
            var y = 0.0;
            var previousInput = 0.0;
            for (var i = 0; i < input.Length; i++)
            {
                y = a * (y + input[i] - previousInput);
                previousInput = input[i];
                output[i] = y;
            }
        }

        return output;
    }
}
