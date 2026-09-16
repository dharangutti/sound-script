using System.Numerics;

namespace SoundScript.Transcription;

public sealed record PolyphonicPitch(int MidiPitch, double Frequency, double FundamentalShare,
    double ResidualShare, double HarmonicSupport);
public sealed record PolyphonicFrame(double Seconds, double Rms, IReadOnlyList<PolyphonicPitch> Pitches,
    double TonalEnergyShare, bool OctaveAmbiguous, string? Rejection);
public sealed record PolyphonicObservations(IReadOnlyList<PolyphonicFrame> Frames, double DurationSeconds,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics);
public sealed record SimultaneousPitchGroup(double StartSeconds, double DurationSeconds, IReadOnlyList<int> Pitches);
public sealed record PolyphonicEvidence(PolyphonicObservations Observations, int DetectedNotes,
    int MaximumSimultaneousNotes, int ChordCount, double MeanActivePolyphony, double PolyphonicCoverage,
    double StableActiveCoverage, double MeanFundamentalShare, double AmbiguousFrameFraction,
    double OctaveAmbiguousFraction, IReadOnlyList<SimultaneousPitchGroup> Groups);

/// <summary>Joint spectral peak analysis with conservative harmonic accounting.
/// This is a bounded local estimator, not instrument recognition or source separation.</summary>
public sealed class PolyphonicAnalyzer
{
    public const int Window = 4096, Hop = 320, MinimumPitch = 36, MaximumPitch = 96, MaximumPolyphony = 6;
    private static readonly double[] Hann = Enumerable.Range(0, Window)
        .Select(i => .5 - .5 * Math.Cos(2 * Math.PI * i / (Window - 1))).ToArray();
    // Energy ceilings relative to a lower fundamental, deliberately not a fitted piano model.
    private static readonly double[] HarmonicCeiling = [0, 1, .28, .12, .06, .025, .008];
    private sealed record Peak(double Frequency, double Energy);

    public PolyphonicObservations Analyze(AnalysisAudio audio, CancellationToken token = default)
        => Complete(audio, Frames(audio, token).ToArray());

    public async Task<PolyphonicObservations> AnalyzeAsync(AnalysisAudio audio, CancellationToken token = default)
    {
        var frames = new List<PolyphonicFrame>();
        foreach (var frame in Frames(audio, token))
        {
            frames.Add(frame);
            if (frames.Count % 10 == 0) await Task.Delay(1, token);
        }
        token.ThrowIfCancellationRequested();
        return Complete(audio, frames);
    }

    private static IEnumerable<PolyphonicFrame> Frames(AnalysisAudio audio, CancellationToken token)
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
        var spectrum = new Complex[Window]; var power = new double[Window / 2];
        var previousPitches = new HashSet<int>();
        for (int frame = 0; frame < rms.Length; frame++)
        {
            token.ThrowIfCancellationRequested();
            double seconds = frame * Hop / (double)AnalysisAudio.SampleRate;
            if (rms[frame] < threshold) { previousPitches.Clear(); yield return new(seconds, rms[frame], [], 0, false, null); continue; }
            int start = frame * Hop + Hop / 2 - Window / 2;
            for (int j = 0; j < Window; j++) spectrum[j] = new(start + j >= 0 && start + j < samples.Length ? samples[start + j] * Hann[j] : 0, 0);
            Transform(spectrum);
            for (int j = 0; j < power.Length; j++) power[j] = spectrum[j].Real * spectrum[j].Real + spectrum[j].Imaginary * spectrum[j].Imaginary;
            double total = power.Sum();
            var peaks = new List<Peak>();
            for (int j = 2; j < power.Length - 1; j++)
            {
                if (power[j] <= power[j - 1] || power[j] < power[j + 1]) continue;
                double energy = power[j - 1] + power[j] + power[j + 1];
                if (energy < total * .006) continue;
                double left = Math.Log(Math.Max(1e-20, power[j - 1])), center = Math.Log(Math.Max(1e-20, power[j])), right = Math.Log(Math.Max(1e-20, power[j + 1]));
                double divisor = left - 2 * center + right;
                double delta = Math.Abs(divisor) < 1e-15 ? 0 : Math.Clamp(.5 * (left - right) / divisor, -.5, .5);
                peaks.Add(new((j + delta) * AnalysisAudio.SampleRate / Window, energy));
            }
            double tonalShare = total <= 1e-15 ? 0 : Math.Min(1, peaks.Sum(p => p.Energy) / total);
            bool Near(double a, double b) => Math.Abs(12 * Math.Log2(a / b)) < .35;
            double EnergyAt(double frequency) => peaks.Where(p => Near(p.Frequency, frequency)).Select(p => p.Energy).DefaultIfEmpty().Max();
            // A strong 2:3:5 family without the lower fundamental is not safe to label
            // as three piano keys. Do not invent the absent fundamental either.
            bool missingFundamental = peaks.Any(p => p.Frequency / 2 >= 65 && p.Energy > total * .08 &&
                EnergyAt(p.Frequency / 2) < total * .006 && EnergyAt(p.Frequency * 1.5) > total * .08 && EnergyAt(p.Frequency * 2.5) > total * .08);
            var candidates = new List<PolyphonicPitch>(); bool octave = false;
            foreach (var peak in peaks.OrderBy(p => p.Frequency))
            {
                int midi = MonophonicAnalyzer.FrequencyToMidi(peak.Frequency);
                double cents = Math.Abs(69 + 12 * Math.Log2(peak.Frequency / 440) - midi);
                if (midi < MinimumPitch || midi > MaximumPitch || cents > .35 || peak.Energy < total * .018) continue;
                double accounted = 0; bool octaveRelated = false;
                foreach (var lower in candidates)
                {
                    int harmonic = (int)Math.Round(peak.Frequency / lower.Frequency);
                    if (harmonic is < 2 or > 6 || !Near(peak.Frequency, lower.Frequency * harmonic)) continue;
                    accounted += lower.FundamentalShare * total * HarmonicCeiling[harmonic];
                    if (harmonic == 2) octaveRelated = true;
                }
                double residual = Math.Max(0, peak.Energy - accounted);
                if (residual < Math.Max(total * .018, peak.Energy * .3)) continue;
                if (octaveRelated)
                {
                    octave = true;
                    // Require energy in an overtone of the upper candidate not readily
                    // explained as a weak sixth partial of the lower candidate.
                    bool continuing = previousPitches.Contains(midi) && peak.Energy > accounted * 1.5;
                    if (!continuing && EnergyAt(peak.Frequency * 3) < Math.Max(total * .006, accounted * .06)) continue;
                }
                double harmonicSupport = Enumerable.Range(2, 3).Count(h => EnergyAt(peak.Frequency * h) > total * .006) / 3.0;
                candidates.Add(new(midi, peak.Frequency, peak.Energy / total, residual / total, harmonicSupport));
            }
            candidates = candidates.GroupBy(p => p.MidiPitch).Select(g => g.OrderByDescending(p => p.ResidualShare).First()).ToList();
            string? rejection = missingFundamental ? "missing-fundamental-family"
                : tonalShare < .7 ? "diffuse-or-percussive-spectrum"
                : candidates.Count > MaximumPolyphony || peaks.Count(p => p.Energy > total * .065 && p.Frequency >= 65 && p.Frequency <= 2100) > MaximumPolyphony ? "dense-unresolved-polyphony"
                : candidates.Count == 0 ? "no-supported-fundamentals" : null;
            previousPitches = rejection == null ? candidates.Select(p => p.MidiPitch).ToHashSet() : [];
            yield return new(seconds, rms[frame], rejection == null ? candidates : [], tonalShare, octave, rejection);
        }
    }

    private static PolyphonicObservations Complete(AnalysisAudio audio, IReadOnlyList<PolyphonicFrame> frames)
    {
        var diagnostics = new List<AnalysisDiagnostic> {
            new("experimental-polyphonic", "Experimental simultaneous fundamentals, not verified piano keys. Parallel voices preserve overlap; harmonic false positives and missed quiet notes remain possible."),
            new("polyphonic-scope", "C2–C7, at most six candidates per frame, 256 ms spectral window. Dense/diffuse frames and strong missing-fundamental families are rejected. Octave candidates require independent upper harmonic evidence.")
        };
        if (!frames.Any(f => f.Rms >= .003)) diagnostics.Add(new("silence", "No audio above the absolute activity threshold."));
        if (audio.Samples.Count(s => Math.Abs(s) >= .999f) > audio.Samples.Length * .001) diagnostics.Add(new("clipping", "More than 0.1% of samples reach full scale."));
        return new(frames, audio.Samples.Length / (double)AnalysisAudio.SampleRate, diagnostics);
    }

    private static void Transform(Complex[] data)
    {
        for (int i = 1, j = 0; i < data.Length; i++)
        {
            int bit = data.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit; if (i < j) (data[i], data[j]) = (data[j], data[i]);
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
                    data[start + j] = even + odd; data[start + j + size / 2] = even - odd; phase *= step;
                }
            }
        }
    }
}
