// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave.Model;

/// <summary>
/// Typed, validated master-effect parameters — the hand-off between the
/// adapter layer (which reads EffectNode from the AST) and the DSP in
/// SoundScript.Wave.Effects (which never sees the AST), mirroring how
/// NoteEvent/TimbreParams decouple synthesis from grammar internals.
/// </summary>
public abstract record EffectSettings;

/// <summary>Feedback delay line: <c>effect delay time=0.25 feedback=0.4 mix=0.3</c>.</summary>
public sealed record DelaySettings(
    double TimeSeconds,   // > 0
    double Feedback,      // [0, 1)
    double Mix            // [0, 1]; 0 = dry only
) : EffectSettings;

public enum FilterKind
{
    LowPass,
    HighPass
}

/// <summary>Single-pole IIR filter: <c>effect filter type=lowpass cutoff=2000</c>.</summary>
public sealed record FilterSettings(
    FilterKind Kind,
    double CutoffHz       // > 0
) : EffectSettings;
