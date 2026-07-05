// UNDER DEVELOPMENT — v1 prototype
namespace SoundScript.Wave.Model;

/// <summary>
/// Naive (non-band-limited) waveform shapes available to <see cref="TimbreParams"/>.
/// Aliasing at high frequencies is a known v1 limitation — see
/// SoundScript.Wave.Synthesis.Oscillator for details.
/// </summary>
public enum OscillatorType
{
    Sine,
    Saw,
    Square,
    Triangle
}
