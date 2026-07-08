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
    Triangle,

    /// <summary>
    /// Deterministic filtered white noise (v2 wavetable path only — see
    /// SoundScript.Wave.Synthesis.NoteRenderer). <c>FrequencyHz</c> is
    /// reinterpreted as a one-pole low-pass cutoff rather than a pitch.
    /// </summary>
    Noise
}
