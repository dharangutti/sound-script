// UNDER DEVELOPMENT — v2
namespace SoundScript.Wave.Model;

/// <summary>
/// Per-note synthesis parameters. <see cref="Pan"/> is honored by the v2
/// stereo mixing path (Mixer.RenderTrackStereo, constant-power law) and
/// still ignored by the unchanged mono path. The grammar has no pan
/// directive — the adapter always emits 0.0 — so non-zero values currently
/// come only from direct API callers; see Mixer.RenderTrackStereo for the
/// scope rationale.
/// </summary>
public record TimbreParams(
    OscillatorType Oscillator,
    Adsr Envelope,
    double DetuneCents = 0.0,
    double Pan = 0.0   // -1.0 (hard left) to 1.0 (hard right)
)
{
    /// <summary>The flat default sound used when a track has no explicit timbre directive.</summary>
    public static TimbreParams Default { get; } = new(OscillatorType.Sine, Adsr.Neutral);
}
