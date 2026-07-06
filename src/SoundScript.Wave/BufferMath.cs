// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave;

/// <summary>
/// Shared sample-buffer utilities: peak scan, clip-safe (down-only,
/// never-boosted) normalization scale, and float&lt;-&gt;double conversion.
/// Used by both <see cref="Mixing.Mixer"/> (track summation) and
/// <see cref="Effects.MasterEffectChain"/> (post-mix effects) so the
/// "scale down only when the mix would clip, otherwise leave quiet passages
/// alone" policy — and the float/double conversion loops — live in one
/// place instead of two independently-maintained copies.
/// </summary>
internal static class BufferMath
{
    internal static double Peak(double[] buffer)
    {
        var peak = 0.0;
        foreach (var sample in buffer)
            peak = Math.Max(peak, Math.Abs(sample));

        return peak;
    }

    internal static double Peak(double[] a, double[] b) => Math.Max(Peak(a), Peak(b));

    /// <summary>Scale down only when the mix would otherwise clip — never boosted (no dynamic range compression).</summary>
    internal static double NormalizationScale(double peak) =>
        peak > 1.0 ? 1.0 / peak : 1.0;

    internal static double[] ToDouble(float[] buffer)
    {
        var result = new double[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = buffer[i];

        return result;
    }

    internal static float[] ToFloat(double[] buffer, double scale = 1.0)
    {
        var result = new float[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = (float)(buffer[i] * scale);

        return result;
    }
}
