using SoundScript.Core;

namespace SoundScript.Transcription;

public sealed record PercussionHit(PercussionSound Sound, double StartSeconds, double StartBeat,
    double DurationBeats, int Velocity, Evidence Classification, double OnsetStrength,
    double LowShare, double MidShare, double HighShare);
public sealed record RhythmTempoHypothesis(int Bpm, double GridFit);
public sealed record PercussionEvidence(IReadOnlyList<PercussionHit> Hits, int RejectedTransients,
    IReadOnlyList<RhythmTempoHypothesis> TempoAlternatives, double GridOriginSeconds,
    string ClassificationMeaning = "Spectral dominance score, not calibrated instrument probability; overlapping hits may merge.");

/// <summary>Independent envelope/transient path. Never invokes pitch analysis or note interpretation.</summary>
public sealed class PercussionTranscriber
{
    private const int Hop = 160;
    private sealed record Frame(double Low, double Mid, double High)
    {
        public double Energy => Low + Mid + High;
    }

    public TranscriptionResult Transcribe(AnalysisAudio audio, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
        => Run(audio, options ?? new(), false, cancellationToken).GetAwaiter().GetResult();

    public Task<TranscriptionResult> TranscribeAsync(AnalysisAudio audio, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
        => Run(audio, options ?? new(), true, cancellationToken);

    private static async Task<TranscriptionResult> Run(AnalysisAudio audio, TranscriptionOptions options, bool yield, CancellationToken token)
    {
        if (options.Tempo is < 20 or > 300) throw new ArgumentOutOfRangeException(nameof(options.Tempo));
        if (options.Roles != null) throw new ArgumentException("Roles are supported only in mixed mode.");
        token.ThrowIfCancellationRequested();
        var frames = new List<Frame>();
        double low = 0, broad = 0;
        double a = 1 - Math.Exp(-2 * Math.PI * 180 / AnalysisAudio.SampleRate);
        double b = 1 - Math.Exp(-2 * Math.PI * 2200 / AnalysisAudio.SampleRate);
        for (int start = 0; start < audio.Samples.Length; start += Hop)
        {
            token.ThrowIfCancellationRequested();
            if (yield && start % (Hop * 20) == 0) await Task.Delay(1, token);
            double l = 0, m = 0, h = 0;
            int count = Math.Min(Hop, audio.Samples.Length - start);
            for (int i = start; i < start + count; i++)
            {
                double x = audio.Samples[i];
                low += a * (x - low); broad += b * (x - broad);
                l += low * low; m += (broad - low) * (broad - low); h += (x - broad) * (x - broad);
            }
            frames.Add(new(l / count, m / count, h / count));
        }
        double maximum = frames.Select(f => f.Energy).DefaultIfEmpty().Max();
        var hits = new List<PercussionHit>();
        int rejected = 0, lastAttack = -8;
        for (int i = 0; i < frames.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            if (yield && i % 100 == 0) await Task.Delay(1, token);
            double baseline = frames.Skip(Math.Max(0, i - 5)).Take(Math.Min(i, 5)).Select(f => f.Energy).DefaultIfEmpty().Average();
            double current = frames[i].Energy;
            if (i - lastAttack < 7 || current < Math.Max(.000025, maximum * .015) || current < baseline * 4 + maximum * .008) continue;
            lastAttack = i;
            // Inspect early attack and its decay. Continuous noise/steady tones are not drum events.
            var attack = frames.Skip(i).Take(3).ToArray();
            double peak = attack.Max(f => f.Energy);
            var late = frames.Skip(i + 7).Take(5).ToArray();
            if (late.Length < 3 || late.Average(f => f.Energy) > peak * .65)
            {
                rejected++;
                continue;
            }
            double total = attack.Sum(f => f.Energy);
            double ls = attack.Sum(f => f.Low) / total, ms = attack.Sum(f => f.Mid) / total, hs = attack.Sum(f => f.High) / total;
            var sound = ls >= .55 ? PercussionSound.Kick : hs >= .60 ? PercussionSound.Hat : ms >= .43 ? PercussionSound.Snare : PercussionSound.Click;
            double dominance = Math.Max(ls, Math.Max(ms, hs));
            double strength = Math.Clamp(1 - baseline / current, 0, 1);
            hits.Add(new(sound, i * .01, 0, .125, (int)Math.Clamp(Math.Round(110 * Math.Sqrt(peak / Math.Max(maximum, .000025))), 35, 110),
                new(sound == PercussionSound.Click ? 0 : dominance, sound == PercussionSound.Click ? "Ambiguous transient: generic unpitched playback" : "Coarse attack-band dominance; not verified drum identity"), strength, ls, ms, hs));
        }
        double origin = hits.Count == 0 ? 0 : hits[0].StartSeconds;
        double Fit(int bpm) => hits.Count < 3 ? 0 : hits.Average(h =>
        {
            double beat = (h.StartSeconds - origin) * bpm / 60;
            double errorSeconds = Math.Abs(beat - Math.Round(beat * 4) / 4) * 60 / bpm;
            return Math.Max(0, 1 - errorSeconds / .04);
        });
        var candidates = Enumerable.Range(40, 201).Select(bpm => new RhythmTempoHypothesis(bpm, Fit(bpm)))
            .OrderByDescending(t => Math.Round(t.GridFit, 8)).ThenBy(t => Math.Abs(t.Bpm - 120)).ToArray();
        int tempo = options.Tempo ?? (hits.Count < 3 ? 120 : candidates[0].Bpm);
        var alternatives = new List<RhythmTempoHypothesis> { new(tempo, Fit(tempo)) };
        foreach (int related in new[] { (int)Math.Round(tempo / 2.0), tempo * 2 }.Where(bpm => hits.Count >= 3 && bpm >= 20 && bpm <= 300))
            if (alternatives.All(t => t.Bpm != related)) alternatives.Add(new(related, Fit(related)));
        foreach (var candidate in candidates.Where(c => hits.Count >= 3 && c.GridFit >= candidates[0].GridFit - .05))
            if (alternatives.All(t => Math.Abs(t.Bpm - candidate.Bpm) >= 15) && alternatives.Count < 5) alternatives.Add(candidate);
        double fit = Fit(tempo);
        var tracks = new List<MusicalTrack>();
        foreach (var group in hits.GroupBy(h => h.Sound))
        {
            var lane = group.Select(h =>
            {
                double beat = h.StartSeconds * tempo / 60;
                double snapped = origin * tempo / 60 + Math.Round((h.StartSeconds - origin) * tempo / 60 * 4) / 4;
                if (options.Quantize && fit >= .8 && Math.Abs(snapped - beat) * 60 / tempo <= .025) beat = snapped;
                return h with { StartBeat = Math.Round(beat, 6) };
            }).ToArray();
            var rests = new List<MusicalRest>(); double cursor = 0;
            for (int j = 0; j < lane.Length; j++)
            {
                double duration = Math.Min(.125, j + 1 < lane.Length ? lane[j + 1].StartBeat - lane[j].StartBeat : .125);
                lane[j] = lane[j] with { DurationBeats = Math.Round(duration, 6) };
                if (lane[j].StartBeat > cursor) rests.Add(new(cursor, lane[j].StartBeat - cursor));
                cursor = lane[j].StartBeat + lane[j].DurationBeats;
            }
            double end = Math.Round(audio.Samples.Length / 16000.0 * tempo / 60, 6);
            if (end > cursor) rests.Add(new(cursor, end - cursor));
            tracks.Add(new(group.Key.ToString().ToLowerInvariant(), "percussion", 0, [], rests) { Percussion = lane });
        }
        var diagnostics = new List<AnalysisDiagnostic>
        {
            new("percussion-only", "Independent transient analysis; no pitched notes or drum-resonance pitches are inferred."),
            new("coarse-classification", "Kick/snare/hat labels are coarse spectral estimates. Ambiguous transients use generic clicks; overlapping hits can merge, and quiet hits may be missed."),
            new("rhythm-tempo", options.Tempo.HasValue ? "Tempo supplied by user; grid fit does not verify meter." : hits.Count < 3 ? "Too few hits to infer tempo: 120 BPM is a playback fallback, not a detected tempo." : "Tempo is a grid hypothesis. Half/double and subdivision alternatives remain ambiguous; no meter is inferred."),
            new("transient-rejections", $"{rejected} candidate transients lacked sufficient decay evidence or observation time."),
            new("synthetic-percussion", "Playback uses a synthetic kit, not isolated source audio. Hit duration controls the score cursor, not measured drum decay.")
        };
        double seconds = audio.Samples.Length / 16000.0;
        return new(new([new(0, tempo, new(fit, options.Tempo.HasValue ? "User tempo" : "Independent onset-grid hypothesis"))], null, null, tracks, [], seconds),
            new([], seconds, diagnostics), 0, fit, diagnostics)
        {
            Percussion = new(tracks.SelectMany(t => t.Percussion!).OrderBy(h => h.StartSeconds).ToArray(), rejected, alternatives, origin)
        };
    }
}
