// UNDER DEVELOPMENT — v1 prototype (legacy — retained for v2 A/B determinism testing only, see WaveEngineOptions)
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Pure, phase-based sample generation — one function per waveform,
/// <c>phase</c> given as a fraction of a cycle (wrapped to [0,1) internally).
///
/// No longer the default path: v2 renders through <see cref="Wavetable"/>;
/// this exists only for transition-period A/B comparison behind
/// <see cref="WaveEngineOptions.UseLegacyTrigOscillator"/> and is slated for
/// removal, not a permanent dual implementation.
///
/// These are naive (non-band-limited) waveforms: Saw, Square, and Triangle
/// contain harmonics above the Nyquist frequency for high fundamental
/// pitches, which will alias (fixed in the v2 wavetable path via per-octave
/// band-limited tables).
/// </summary>
public static class Oscillator
{
    public static double Sample(OscillatorType type, double phase)
    {
        var p = phase - Math.Floor(phase);

        return type switch
        {
            OscillatorType.Sine => Math.Sin(2.0 * Math.PI * p),
            OscillatorType.Saw => 2.0 * p - 1.0,
            OscillatorType.Square => p < 0.5 ? 1.0 : -1.0,
            OscillatorType.Triangle => 4.0 * Math.Abs(p - 0.5) - 1.0,
            _ => 0.0
        };
    }
}
