using System.Collections.Immutable;
using SoundScript.Labs.Execution;
using SoundScript.Labs.IR;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Labs.Analysis;

public sealed record SpectralPeak(double FrequencyHz, double Amplitude);
public sealed record FftSummary(int InputSamples, int FftSize, double BinWidthHz, string Window);
public sealed record AnalysisResult(double? Rms, double? PeakFrequencyHz,
    FftSummary? Fft, ImmutableArray<SpectralPeak> Peaks);

public static class AnalysisEngine
{
    public static AnalysisResult Analyze(ExperimentIr experiment, SampleBuffer buffer)
    {
        buffer.ValidateFor(experiment);
        double? rms = null;
        if (experiment.Analysis.HasFlag(AnalysisKind.Rms))
        {
            double energy = 0;
            foreach (var sample in buffer.Samples) energy += (double)sample * sample;
            rms = Math.Sqrt(energy / buffer.Samples.Length);
        }
        if ((experiment.Analysis & (AnalysisKind.Fft | AnalysisKind.Peaks | AnalysisKind.Frequency)) == 0)
            return new(rms, null, null, []);

        var magnitudes = Spectrum(buffer.Samples);
        int size = (magnitudes.Length - 1) * 2;
        double binWidth = buffer.SampleRate / (double)size;
        var candidates = new List<SpectralPeak>();
        double maximum = magnitudes.Skip(1).Max();
        // Exclude DC; deterministic local maxima, descending amplitude then ascending frequency.
        double threshold = Math.Max(1e-12, maximum * 1e-6);
        for (int i = 1; i < magnitudes.Length; i++)
        {
            if (magnitudes[i] > threshold && magnitudes[i] > magnitudes[i - 1] &&
                (i == magnitudes.Length - 1 || magnitudes[i] >= magnitudes[i + 1]))
                candidates.Add(new(i * binWidth, magnitudes[i]));
        }
        var peaks = candidates.OrderByDescending(x => x.Amplitude).ThenBy(x => x.FrequencyHz).Take(8).ToImmutableArray();
        double? dominant = experiment.Analysis.HasFlag(AnalysisKind.Frequency) && peaks.Length > 0
            ? peaks[0].FrequencyHz : null;
        return new(rms, dominant, new(buffer.Samples.Length, size, binWidth, "rectangular; zero-padded"),
            experiment.Analysis.HasFlag(AnalysisKind.Peaks) ? peaks : []);
    }

    // Radix-2 FFT, full input, next-power-of-two zero padding. No sampling/truncation.
    // Single-sided amplitude: 2*|X|/inputCount, except DC and Nyquist use |X|/inputCount.
    public static ImmutableArray<double> Spectrum(ImmutableArray<float> samples)
    {
        if (samples.IsDefaultOrEmpty || samples.Length > ExperimentIr.MaxSamples || samples.Any(x => !float.IsFinite(x)))
            throw new ArgumentException("Invalid FFT sample buffer.");
        int size = 2;
        while (size < samples.Length) size <<= 1;
        var real = new double[size];
        var imaginary = new double[size];
        for (int i = 0; i < samples.Length; i++) real[i] = samples[i];
        for (int i = 1, j = 0; i < size; i++)
        {
            int bit = size >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (real[i], real[j]) = (real[j], real[i]);
        }
        for (int length = 2; length <= size; length <<= 1)
        {
            int half = length / 2;
            for (int offset = 0; offset < half; offset++)
            {
                double angle = -2 * Math.PI * offset / length;
                double cosine = DeterministicMath.Cos(angle), sine = DeterministicMath.Sin(angle);
                for (int start = 0; start < size; start += length)
                {
                    int left = start + offset, right = left + half;
                    double tr = cosine * real[right] - sine * imaginary[right];
                    double ti = sine * real[right] + cosine * imaginary[right];
                    real[right] = real[left] - tr;
                    imaginary[right] = imaginary[left] - ti;
                    real[left] += tr;
                    imaginary[left] += ti;
                }
            }
        }
        var magnitudes = ImmutableArray.CreateBuilder<double>(size / 2 + 1);
        for (int i = 0; i <= size / 2; i++)
        {
            double scale = i == 0 || i == size / 2 ? 1 : 2;
            magnitudes.Add(scale * Math.Sqrt(real[i] * real[i] + imaginary[i] * imaginary[i]) / samples.Length);
        }
        return magnitudes.MoveToImmutable();
    }
}
