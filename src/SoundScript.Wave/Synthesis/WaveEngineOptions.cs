// UNDER DEVELOPMENT — v2
namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Transition-period engine switches. Internal on purpose: only
/// SoundScript.Tests (via InternalsVisibleTo) may touch this — nothing
/// public exposes it, so it cannot become an accidental API surface.
/// </summary>
internal static class WaveEngineOptions
{
    /// <summary>
    /// When true, <see cref="NoteRenderer"/> falls back to the v1
    /// Math.Sin/float-phase <see cref="Oscillator"/> instead of the v2
    /// wavetable path — retained solely for A/B determinism comparison while
    /// v2 soaks, per the v2 spec's instruction that the legacy oscillator is
    /// not a permanent dual implementation. Default is false (wavetable).
    /// </summary>
    internal static bool UseLegacyTrigOscillator = false;
}
