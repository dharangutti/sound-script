// UNDER DEVELOPMENT — v1 prototype
namespace SoundScript.Wave.Model;

/// <summary>
/// Per-note synthesis parameters. <see cref="Pan"/> exists for the model
/// shape but is intentionally ignored by the v1 WAV writer, which is
/// mono-only — stereo panning is a v2 concern.
/// </summary>
public record TimbreParams(
    OscillatorType Oscillator,
    Adsr Envelope,
    double DetuneCents = 0.0,
    double Pan = 0.0   // -1.0 to 1.0; unused until stereo output ships
)
{
    /// <summary>The flat default sound used when a track has no explicit timbre directive.</summary>
    public static TimbreParams Default { get; } = new(OscillatorType.Sine, Adsr.Neutral);
}
