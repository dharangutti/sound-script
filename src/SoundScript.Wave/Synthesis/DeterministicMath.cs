// UNDER DEVELOPMENT — v2
namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Sine/cosine built from IEEE-754 primitives only — <c>+ - * /</c>, the
/// exact operations <see cref="Math.Floor(double)"/>, and the compile-time
/// constant <see cref="Math.PI"/> — via a Taylor series after range reduction
/// to [-π, π).
///
/// Why not <see cref="Math.Sin(double)"/>/<see cref="Math.Cos(double)"/>:
/// IEEE 754 guarantees correct rounding (bit-identical results on every
/// compliant platform) for <c>+ - * /</c> and <see cref="Math.Sqrt(double)"/>,
/// but NOT for transcendental functions — Math.Sin/Math.Cos defer to the
/// platform's libm/intrinsics and may differ in the last bits across
/// OS/CPU/runtime. Precomputing wavetables with Math.Sin would not close
/// that gap: it would merely move the platform-dependent call from millions
/// of per-sample invocations to a handful at table-build time, and the
/// divergent table would then silently poison every sample of every render
/// on that platform, breaking the byte-identical output guarantee. Building
/// tables from this class instead makes the tables — and therefore the
/// rendered audio — bit-identical everywhere.
///
/// Accuracy: 16 series terms leave a truncation error far below one double
/// ulp at |x| = π (the worst case after reduction). This class is used only
/// at static wavetable-build time (see <see cref="Wavetable"/>), never in
/// the per-sample hot path.
/// </summary>
public static class DeterministicMath
{
    // Compile-time constant folding of 2·Math.PI — identical bits everywhere.
    private const double TwoPi = 2.0 * Math.PI;

    // Terms beyond the leading one. 16 gives terms through x^33/33! (sin) and
    // x^32/32! (cos); at |x| = π the last term is ~1e-19 — below double ulp.
    private const int SeriesTerms = 16;

    public static double Sin(double x)
    {
        var r = ReduceToPlusMinusPi(x);
        var negativeXSquared = -(r * r);
        var term = r;
        var sum = r;
        for (var k = 1; k <= SeriesTerms; k++)
        {
            term *= negativeXSquared / ((2 * k) * (2 * k + 1));
            sum += term;
        }

        return sum;
    }

    public static double Cos(double x)
    {
        var r = ReduceToPlusMinusPi(x);
        var negativeXSquared = -(r * r);
        var term = 1.0;
        var sum = 1.0;
        for (var k = 1; k <= SeriesTerms; k++)
        {
            term *= negativeXSquared / ((2 * k - 1) * (2 * k));
            sum += term;
        }

        return sum;
    }

    /// <summary>
    /// Maps any angle to [-π, π) for the series. Math.Floor is an exact
    /// IEEE-754 operation, so the reduction is deterministic; for very large
    /// |x| the multiply/subtract rounding error grows, but it grows
    /// identically on every platform (callers keep arguments small anyway —
    /// see Wavetable's exact integer-fraction phase construction).
    /// </summary>
    private static double ReduceToPlusMinusPi(double x)
    {
        var turns = x / TwoPi;
        var wrapped = turns - Math.Floor(turns + 0.5); // [-0.5, 0.5)
        return wrapped * TwoPi;
    }
}
