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
/// After the chain runs, the buffer is peak-normalized DOWN only (same
/// policy as Mixer: never boosted, no compression) because feedback echoes
/// summed onto the dry signal can exceed the mixer's already-normalized peak.
/// Stereo channels are processed with independent effect state but share one
/// normalization scale, computed from the combined peak, so the stereo image
/// is preserved (same rationale as Mixer.MixTracksStereo).
/// </summary>
public static class MasterEffectChain
{
    public static float[] Apply(float[] buffer, IReadOnlyList<EffectSettings> effects, int sampleRate)
    {
        if (effects.Count == 0 || buffer.Length == 0)
            return buffer;

        var processed = ApplyChain(ToDouble(buffer), effects, sampleRate);
        var scale = NormalizationScale(Peak(processed));
        return ToFloat(processed, scale);
    }

    public static (float[] Left, float[] Right) ApplyStereo(
        float[] left, float[] right, IReadOnlyList<EffectSettings> effects, int sampleRate)
    {
        if (effects.Count == 0 || (left.Length == 0 && right.Length == 0))
            return (left, right);

        // Independent state per channel (each gets its own delay line /
        // filter memory); identical chain + identical lengths in, so the
        // channels stay sample-aligned.
        var processedLeft = ApplyChain(ToDouble(left), effects, sampleRate);
        var processedRight = ApplyChain(ToDouble(right), effects, sampleRate);

        var scale = NormalizationScale(Math.Max(Peak(processedLeft), Peak(processedRight)));
        return (ToFloat(processedLeft, scale), ToFloat(processedRight, scale));
    }

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

    private static double Peak(double[] buffer)
    {
        var peak = 0.0;
        foreach (var sample in buffer)
            peak = Math.Max(peak, Math.Abs(sample));

        return peak;
    }

    // Scale down only when the chain would otherwise clip — same policy as
    // Mixer.NormalizePeak (quiet output is left alone).
    private static double NormalizationScale(double peak) =>
        peak > 1.0 ? 1.0 / peak : 1.0;

    private static double[] ToDouble(float[] buffer)
    {
        var result = new double[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = buffer[i];

        return result;
    }

    private static float[] ToFloat(double[] buffer, double scale)
    {
        var result = new float[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = (float)(buffer[i] * scale);

        return result;
    }
}
