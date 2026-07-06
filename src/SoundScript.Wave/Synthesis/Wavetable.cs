// UNDER DEVELOPMENT — v2
using SoundScript.Wave.Io;
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Precomputed single-cycle wavetables plus a 32-bit fixed-point phase
/// accumulator — the v2 replacement for per-sample <c>Math.Sin</c> (see
/// <see cref="DeterministicMath"/> for why runtime trig threatens the
/// byte-identical output guarantee). Every table is built exactly once at
/// static init, from DeterministicMath only; the per-sample path is pure
/// integer addition plus one linear interpolation.
///
/// Band-limiting: naive Saw/Square/Triangle carry harmonics above Nyquist at
/// high pitches and alias — the documented v1 defect. Each of those waveforms
/// therefore gets one table per octave band, built as a truncated Fourier
/// series that stops at the highest harmonic a note at that band's anchor
/// frequency can sound below Nyquist. Sine needs no banding: a lone sinusoid
/// has no harmonics to alias by construction, so it uses a single table at
/// all frequencies.
/// </summary>
public static class Wavetable
{
    /// <summary>Entries per single-cycle table. Must stay equal to 1 &lt;&lt; <see cref="IndexBits"/>.</summary>
    public const int TableSize = 4096;

    // 32-bit phase layout: the top IndexBits select the table entry, the low
    // FractionBits are the linear-interpolation weight between it and the
    // next entry.
    private const int IndexBits = 12;
    private const int FractionBits = 32 - IndexBits;
    private const uint FractionMask = (1u << FractionBits) - 1u;
    private const double FractionScale = 1u << FractionBits;

    // Reuse the writer's sample rate rather than hardcoding 44100 a second
    // time — one source of truth for the Nyquist limit below.
    private static readonly double NyquistHz = WavWriter.SampleRate / 2.0;

    /// <summary>
    /// Octave-band anchor frequencies (Hz) for the band-limited waveforms.
    /// A note uses the smallest anchor ≥ its frequency, and that band's table
    /// only contains harmonics n with anchor·n ≤ Nyquist — so the note's own
    /// highest harmonic (frequency·n ≤ anchor·n) can never exceed Nyquist.
    /// </summary>
    private static readonly double[] BandAnchorsHz =
        [32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 22050];

    // Built once, in declaration order (anchors/Nyquist above must stay
    // declared before these), from DeterministicMath only.
    private static readonly double[] SineTable = BuildSineTable();
    private static readonly double[][] SawTables = BuildBandTables(BuildSawTable);
    private static readonly double[][] SquareTables = BuildBandTables(BuildSquareTable);
    private static readonly double[][] TriangleTables = BuildBandTables(BuildTriangleTable);

    /// <summary>
    /// Returns the single-cycle table for a waveform at the given (detuned)
    /// note frequency. Band selection is a plain numeric comparison —
    /// deterministic. Sine ignores the frequency entirely (no banding).
    /// </summary>
    public static double[] GetTable(OscillatorType type, double frequencyHz) => type switch
    {
        OscillatorType.Sine => SineTable,
        OscillatorType.Saw => SawTables[BandIndexFor(frequencyHz)],
        OscillatorType.Square => SquareTables[BandIndexFor(frequencyHz)],
        OscillatorType.Triangle => TriangleTables[BandIndexFor(frequencyHz)],
        _ => SineTable
    };

    /// <summary>
    /// Fixed-point phase step for one output sample, computed ONCE per note
    /// (basic arithmetic plus the exact operations Math.Floor/Math.Round —
    /// deterministic). Per-sample advancement is then pure uint addition,
    /// which wraps modulo 2^32 — well-defined and identical on every
    /// platform — replacing v1's float phase accumulation and its
    /// platform-drift risk.
    /// </summary>
    public static uint PhaseIncrement(double frequencyHz, int sampleRate)
    {
        var cyclesPerSample = frequencyHz / sampleRate;
        // Fold frequencies at/above the sample rate into [0, 1) so the cast
        // below is always in range (same aliasing semantics, never undefined).
        cyclesPerSample -= Math.Floor(cyclesPerSample);
        return unchecked((uint)(long)Math.Round(cyclesPerSample * 4294967296.0));
    }

    /// <summary>
    /// Samples a table at a 32-bit phase. The linear interpolation between
    /// adjacent entries is the one remaining source of sub-sample variation,
    /// and it is still fully deterministic: pure <c>+ - * /</c> on the same
    /// inputs yields bit-identical output on every IEEE-754 platform.
    /// </summary>
    public static double Sample(double[] table, uint phase)
    {
        var index = (int)(phase >> FractionBits);
        var fraction = (phase & FractionMask) / FractionScale;
        var a = table[index];
        var b = table[(index + 1) & (TableSize - 1)];
        return a + (b - a) * fraction;
    }

    private static int BandIndexFor(double frequencyHz)
    {
        for (var i = 0; i < BandAnchorsHz.Length; i++)
        {
            if (BandAnchorsHz[i] >= frequencyHz)
                return i;
        }

        // Above the top anchor (≥ Nyquist): clamp to the last, most
        // heavily band-limited table.
        return BandAnchorsHz.Length - 1;
    }

    private static double[][] BuildBandTables(Func<int, double[]> buildForHarmonicCount)
    {
        var tables = new double[BandAnchorsHz.Length][];
        for (var band = 0; band < BandAnchorsHz.Length; band++)
        {
            var harmonicCount = Math.Max(1, (int)Math.Floor(NyquistHz / BandAnchorsHz[band]));
            tables[band] = buildForHarmonicCount(harmonicCount);
        }

        return tables;
    }

    private static double[] BuildSineTable()
    {
        var table = new double[TableSize];
        for (var i = 0; i < TableSize; i++)
            table[i] = SinCycles(1, i);

        return table;
    }

    // Saw Fourier series: (2/π) · Σ (-1)^(n+1)/n · sin(2π·n·t), n = 1..H.
    // The analytic prefactor is irrelevant here — NormalizePeak rescales the
    // finished table to a ±1.0 peak (matching the naive waveforms' range),
    // which also absorbs any truncated-series scaling error.
    private static double[] BuildSawTable(int harmonicCount)
    {
        var table = new double[TableSize];
        for (var i = 0; i < TableSize; i++)
        {
            var sum = 0.0;
            for (var n = 1; n <= harmonicCount; n++)
            {
                var sign = n % 2 == 1 ? 1.0 : -1.0;
                sum += sign / n * SinCycles(n, i);
            }

            table[i] = sum;
        }

        return NormalizePeak(table);
    }

    // Square Fourier series (odd harmonics only): (4/π) · Σ sin(2π·n·t)/n.
    private static double[] BuildSquareTable(int harmonicCount)
    {
        var table = new double[TableSize];
        for (var i = 0; i < TableSize; i++)
        {
            var sum = 0.0;
            for (var n = 1; n <= harmonicCount; n += 2)
                sum += SinCycles(n, i) / n;

            table[i] = sum;
        }

        return NormalizePeak(table);
    }

    // Triangle Fourier series (odd harmonics, alternating sign, 1/n²):
    // (8/π²) · Σ (-1)^((n-1)/2)/n² · cos(2π·n·t).
    private static double[] BuildTriangleTable(int harmonicCount)
    {
        var table = new double[TableSize];
        for (var i = 0; i < TableSize; i++)
        {
            var sum = 0.0;
            for (var n = 1; n <= harmonicCount; n += 2)
            {
                var sign = (n - 1) / 2 % 2 == 0 ? 1.0 : -1.0;
                sum += sign / (n * n) * CosCycles(n, i);
            }

            table[i] = sum;
        }

        return NormalizePeak(table);
    }

    // sin/cos of 2π·(harmonic·index/TableSize). harmonic·index is an exact
    // integer and TableSize a power of two, so the integer-modulo fraction
    // below is exact — the series argument stays in [0, 2π) with full
    // precision instead of feeding huge angles into range reduction.
    private static double SinCycles(int harmonic, int index)
    {
        var cycles = (double)((long)harmonic * index % TableSize) / TableSize;
        return DeterministicMath.Sin(2.0 * Math.PI * cycles);
    }

    private static double CosCycles(int harmonic, int index)
    {
        var cycles = (double)((long)harmonic * index % TableSize) / TableSize;
        return DeterministicMath.Cos(2.0 * Math.PI * cycles);
    }

    private static double[] NormalizePeak(double[] table)
    {
        var peak = 0.0;
        foreach (var value in table)
            peak = Math.Max(peak, Math.Abs(value));

        if (peak <= 0.0)
            return table;

        var scale = 1.0 / peak;
        for (var i = 0; i < table.Length; i++)
            table[i] *= scale;

        return table;
    }
}
