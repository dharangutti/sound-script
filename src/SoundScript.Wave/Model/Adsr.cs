// UNDER DEVELOPMENT — v1 prototype
namespace SoundScript.Wave.Model;

/// <summary>
/// Attack/Decay/Sustain/Release envelope shape, in physical units (seconds for
/// the time segments, 0.0-1.0 level for sustain). See
/// SoundScript.Wave.Synthesis.Envelope for how this is evaluated per-sample.
/// </summary>
public record Adsr(
    double Attack,   // seconds
    double Decay,    // seconds
    double Sustain,  // level 0.0-1.0
    double Release   // seconds
)
{
    /// <summary>Neutral fallback envelope used when a track has no explicit timbre directive.</summary>
    public static Adsr Neutral { get; } = new(Attack: 0.01, Decay: 0.05, Sustain: 0.8, Release: 0.1);
}
