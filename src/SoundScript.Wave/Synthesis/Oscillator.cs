// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Pure, phase-based sample generation — one function per waveform,
/// <c>phase</c> given as a fraction of a cycle (wrapped to [0,1) internally).
///
/// These are naive (non-band-limited) waveforms: Saw, Square, and Triangle
/// contain harmonics above the Nyquist frequency for high fundamental
/// pitches, which will alias. Anti-aliased (e.g. PolyBLEP) oscillators are a
/// documented v1 non-goal, not a blocker — flagged here rather than fixed.
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
