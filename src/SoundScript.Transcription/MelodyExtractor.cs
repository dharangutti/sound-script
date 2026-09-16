using System.Numerics;

namespace SoundScript.Transcription;

public sealed record MelodyAnalysis(MusicalObservations Observations, MelodyExtractionEvidence Evidence);

/// <summary>Local deterministic dominant spectral line extraction, not source separation.
/// A peak must own at least 35% of spectral energy and exceed every competing peak
/// by 2.5x in energy. Ambiguous sections remain unpitched.</summary>
public sealed class MelodyExtractor
{
    private const int Window = 2048, Hop = 160;
    private static readonly double[] Hann = Enumerable.Range(0, Window)
        .Select(i => .5 - .5 * Math.Cos(2 * Math.PI * i / (Window - 1))).ToArray();
    private sealed record Frame(PitchFrame Pitch, string? Rejection, bool Competing, bool Octave);

    public MelodyAnalysis Analyze(AnalysisAudio audio, CancellationToken cancellationToken = default)
        => Complete(audio, Frames(audio, cancellationToken).ToArray());

    public async Task<MelodyAnalysis> AnalyzeAsync(AnalysisAudio audio, CancellationToken cancellationToken = default)
    {
        var frames = new List<Frame>();
        foreach (var frame in Frames(audio, cancellationToken))
        {
            frames.Add(frame);
            if (frames.Count % 20 == 0) await Task.Delay(1, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Complete(audio, frames);
    }

    private static IEnumerable<Frame> Frames(AnalysisAudio audio, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var samples = audio.Samples;
        var rms = new double[(samples.Length + Hop - 1) / Hop];
        for (int i = 0; i < rms.Length; i++)
        {
            double sum = 0; int end = Math.Min(samples.Length, (i + 1) * Hop);
            for (int j = i * Hop; j < end; j++) sum += samples[j] * samples[j];
            rms[i] = Math.Sqrt(sum / (end - i * Hop));
        }
        double threshold = Math.Max(.003, rms.DefaultIfEmpty().Max() * .035);
        var spectrum = new Complex[Window];
        var power = new double[Window / 2];
        for (int i = 0; i < rms.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            if (rms[i] < threshold)
            {
                yield return new(new(i * .01, null, 0, rms[i]), null, false, false);
                continue;
            }
            int start = i * Hop + Hop / 2 - Window / 2;
            for (int j = 0; j < Window; j++)
                spectrum[j] = new(start + j >= 0 && start + j < samples.Length ? samples[start + j] * Hann[j] : 0, 0);
            Transform(spectrum);
            for (int j = 0; j < power.Length; j++) power[j] = spectrum[j].Magnitude * spectrum[j].Magnitude;
            double total = power.Sum();
            var peaks = new List<(double Frequency, double Energy)>();
            // Include out-of-range competitors so strong bass/high instruments cannot be hidden.
            for (int j = 2; j < power.Length - 1; j++)
            {
                if (power[j] <= power[j - 1] || power[j] < power[j + 1]) continue;
                double left = Math.Log(Math.Max(1e-20, power[j - 1])), center = Math.Log(Math.Max(1e-20, power[j])), right = Math.Log(Math.Max(1e-20, power[j + 1]));
                double divisor = left - 2 * center + right;
                double delta = Math.Abs(divisor) < 1e-15 ? 0 : Math.Clamp(.5 * (left - right) / divisor, -.5, .5);
                peaks.Add(((j + delta) * AnalysisAudio.SampleRate / Window, power[j - 1] + power[j] + power[j + 1]));
            }
            var sorted = peaks.OrderByDescending(p => p.Energy).ThenBy(p => p.Frequency).ToArray();
            var best = sorted.FirstOrDefault();
            double share = total <= 1e-15 ? 0 : Math.Clamp(best.Energy / total, 0, 1);
            bool competing = sorted.Skip(1).Any(p => p.Energy >= best.Energy * .4);
            bool octave = sorted.Skip(1).Any(p => p.Energy >= best.Energy * .25 &&
                Math.Abs(Math.Abs(12 * Math.Log2(p.Frequency / best.Frequency)) - 12) < .7);
            string? rejection = best.Frequency < 65 || best.Frequency > 1500 ? "outside-pitch-range"
                : share < .35 ? "diffuse-spectrum" : octave ? "octave-uncertain" : competing ? "competing-pitches" : null;
            yield return new(new PitchFrame(i * .01, rejection == null ? best.Frequency : null, 0, rms[i]) { SpectralShare = share }, rejection, competing, octave);
        }
    }

    private static MelodyAnalysis Complete(AnalysisAudio audio, IReadOnlyList<Frame> frames)
    {
        double duration = audio.Samples.Length / (double)AnalysisAudio.SampleRate;
        double threshold = Math.Max(.003, frames.Select(f => f.Pitch.Rms).DefaultIfEmpty().Max() * .035);
        var active = frames.Where(f => f.Pitch.Rms >= threshold).ToArray();
        var rejected = new List<RejectedMelodySection>();
        foreach (var f in frames.Where(f => f.Rejection != null))
        {
            double end = Math.Min(duration, f.Pitch.Seconds + .01);
            if (rejected.Count > 0 && rejected[^1].Reason == f.Rejection && Math.Abs(rejected[^1].EndSeconds - f.Pitch.Seconds) < 1e-8)
                rejected[^1] = rejected[^1] with { EndSeconds = end };
            else rejected.Add(new(f.Pitch.Seconds, end, f.Rejection!));
        }
        var diagnostics = new List<AnalysisDiagnostic> {
            new("experimental-melody", "Experimental dominant spectral line only; accompaniment is discarded. Spectral share measures energy concentration, not periodicity or probability of a correct melody."),
            new("melody-scope", "65–1500 Hz; equal voices, strong harmonics, octave doubling and diffuse mixtures can be rejected. The loudest line need not be the intended melody.")
        };
        if (active.Length == 0) diagnostics.Add(new("silence", "No audio above the analysis silence threshold."));
        if (duration < .1) diagnostics.Add(new("short-input", "Less than 100 ms; insufficient melody evidence."));
        if (audio.Samples.Count(s => Math.Abs(s) >= .999f) > audio.Samples.Length * .001)
            diagnostics.Add(new("clipping", "More than 0.1% of samples reach full scale."));
        double Fraction(Func<Frame, bool> predicate) => active.Length == 0 ? 0 : active.Count(predicate) / (double)active.Length;
        return new(new(frames.Select(f => f.Pitch).ToArray(), duration, diagnostics),
            new(0, 0, active.Select(f => f.Pitch.SpectralShare ?? 0).DefaultIfEmpty().Average(),
                Fraction(f => f.Competing), Fraction(f => f.Octave), rejected));
    }

    private static void Transform(Complex[] data)
    {
        for (int i = 1, j = 0; i < data.Length; i++)
        {
            int bit = data.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }
        for (int size = 2; size <= data.Length; size <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / size);
            for (int start = 0; start < data.Length; start += size)
            {
                var phase = Complex.One;
                for (int j = 0; j < size / 2; j++)
                {
                    var even = data[start + j]; var odd = phase * data[start + j + size / 2];
                    data[start + j] = even + odd; data[start + j + size / 2] = even - odd;
                    phase *= step;
                }
            }
        }
    }
}
