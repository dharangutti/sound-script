// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave.Synthesis;

/// <summary>
/// The single seeded PRNG for every source of "human variation" in the wave
/// pipeline — humanize jitter (timing/velocity) and prosody tone variation
/// both draw from here, per the v3 spec's "build it once, don't duplicate"
/// instruction.
///
/// Stateless by design: each value is a pure function of (seed, index, salt),
/// hashed through the "triple32" integer finalizer (same avalanche rationale
/// as SoundScript.Midi.HumanizeApplicator's StableHash) and divided by 2^32.
/// Only integer ops and one IEEE-exact double division are involved, so the
/// same inputs produce bit-identical doubles on every OS/CPU/runtime.
///
/// Deliberately NOT System.Random: its seeded algorithm is an implementation
/// detail that has already changed between .NET versions, which would break
/// the byte-identical-across-platforms guarantee the safeguards doc demands.
/// </summary>
internal static class DeterministicRandom
{
    /// <summary>Uniform value in [-1, 1) for (seed, index, salt).</summary>
    internal static double Unit(int seed, int index, int salt) =>
        Unit01(seed, index, salt) * 2.0 - 1.0;

    /// <summary>Uniform value in [0, 1) for (seed, index, salt).</summary>
    internal static double Unit01(int seed, int index, int salt) =>
        Hash(seed, index, salt) / 4294967296.0; // exact: uint / 2^32

    /// <summary>
    /// Derives a stable seed from text (FNV-1a 32-bit over UTF-16 code units)
    /// for directives that omit an explicit seed= parameter — variation is
    /// always derived from file content, never wall-clock or unseeded Random
    /// (safeguards doc determinism rule). Callers normalize casing themselves
    /// where names are case-insensitive.
    /// </summary>
    internal static int DeriveSeed(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in text)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    private static uint Hash(int seed, int index, int salt)
    {
        unchecked
        {
            var h = Mix((uint)seed);
            h = Mix(h ^ (uint)index);
            h = Mix(h ^ (uint)salt);
            return h;
        }
    }

    // "triple32" finalizer — strong avalanche on small integer inputs, so
    // consecutive note indexes don't produce correlated jitter.
    private static uint Mix(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
