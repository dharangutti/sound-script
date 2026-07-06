// UNDER DEVELOPMENT — v3
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Effects;

/// <summary>
/// Applies the program's effects, in file order, to the master buffer after
/// the final mix-down.
///
/// Master-only is a deliberate v3 decision (per-track effect routing stays in
/// the parking lot): it keeps the effects stage a pure post-process — nothing
/// upstream (NoteEvent, TimbreParams, adapter, mixer) changes shape — and one
/// master chain is sufficient to prove the grammar + deterministic-DSP
/// mechanism that per-track routing would later reuse.
///
/// After the chain runs, the buffer is peak-normalized DOWN only (via the
/// shared <see cref="BufferMath"/>, same policy as <see cref="Mixing.Mixer"/>:
/// never boosted, no compression) because feedback echoes summed onto the
/// dry signal can exceed the mixer's already-normalized peak. Stereo
/// channels are processed with independent effect state but share one
/// normalization scale, computed from the combined peak, so the stereo image
/// is preserved (same rationale as Mixer.MixTracksStereo).
/// </summary>
public static class MasterEffectChain
{
    public static float[] Apply(float[] buffer, IReadOnlyList<EffectSettings> effects, int sampleRate)
    {
        if (effects.Count == 0 || buffer.Length == 0)
            return buffer;

        var processed = ApplyChain(BufferMath.ToDouble(buffer), effects, sampleRate);
        var scale = BufferMath.NormalizationScale(BufferMath.Peak(processed));
        return BufferMath.ToFloat(processed, scale);
    }

    public static (float[] Left, float[] Right) ApplyStereo(
        float[] left, float[] right, IReadOnlyList<EffectSettings> effects, int sampleRate)
    {
        if (effects.Count == 0 || (left.Length == 0 && right.Length == 0))
            return (left, right);

        // Independent state per channel (each gets its own delay line /
        // filter memory); identical chain + identical lengths in, so the
        // channels stay sample-aligned.
        var processedLeft = ApplyChain(BufferMath.ToDouble(left), effects, sampleRate);
        var processedRight = ApplyChain(BufferMath.ToDouble(right), effects, sampleRate);

        var scale = BufferMath.NormalizationScale(BufferMath.Peak(processedLeft, processedRight));
        return (BufferMath.ToFloat(processedLeft, scale), BufferMath.ToFloat(processedRight, scale));
    }

    // Dispatches on EffectSettings' runtime type rather than the
    // SoundScript.Core.Ast.EffectKinds name (this layer never sees EffectNode
    // or its string Kind — see the class summary), so it can't share that
    // enum directly. WaveV3Tests iterates EffectKinds.All through the full
    // pipeline (parse -> EffectSettingsFactory -> here) as a synchronization
    // check: forgetting a case here for a kind the factory already produces
    // fails that test immediately instead of only at render time.
    private static double[] ApplyChain(double[] buffer, IReadOnlyList<EffectSettings> effects, int sampleRate)
    {
        foreach (var effect in effects)
        {
            buffer = effect switch
            {
                DelaySettings delay => DelayEffect.Process(buffer, delay, sampleRate),
                FilterSettings filter => OnePoleFilter.Process(buffer, filter, sampleRate),
                _ => throw new NotSupportedException(
                    $"Unknown effect settings type '{effect.GetType().Name}'.")
            };
        }

        return buffer;
    }
}
