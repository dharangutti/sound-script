namespace SoundScript.Transcription;

public sealed record PolyphonicMetrics(int ReferenceNotes, int DetectedNotes, int MatchedNotes,
    int MissedNotes, int ExtraNotes, int OctaveErrors, double NotePrecision, double NoteRecall,
    double? OnsetMaeSeconds, double? DurationMaeSeconds, double ChordTonePrecision,
    double ChordToneRecall, double? SimultaneousGroupingAgreement);
public sealed record PolyphonicRoundTripResult(string Source, byte[] PreviewWave, PolyphonicMetrics ScoreToRenderedAudio);

public static class PolyphonicComparison
{
    /// <summary>One-to-one same-pitch matching within 150 ms; pitch-set metrics are
    /// integrated over physical time. No chord root or instrument identity is inferred.</summary>
    public static PolyphonicMetrics Compare(IReadOnlyList<MusicalNote> reference, IReadOnlyList<MusicalNote> detected)
    {
        var candidates = (from r in Enumerable.Range(0, reference.Count)
            from d in Enumerable.Range(0, detected.Count)
            let delta = Math.Abs(reference[r].StartSeconds - detected[d].StartSeconds)
            where reference[r].MidiPitch == detected[d].MidiPitch && delta <= .15000001
            orderby delta, r, d select (R: r, D: d)).ToArray();
        var usedR = new HashSet<int>(); var usedD = new HashSet<int>(); var pairs = new List<(int R, int D)>();
        foreach (var p in candidates) if (!usedR.Contains(p.R) && !usedD.Contains(p.D)) { usedR.Add(p.R); usedD.Add(p.D); pairs.Add(p); }
        int octaves = 0; var octaveD = new HashSet<int>();
        foreach (var r in Enumerable.Range(0, reference.Count).Where(r => !usedR.Contains(r)))
        {
            int d = Enumerable.Range(0, detected.Count).FirstOrDefault(d => !usedD.Contains(d) && !octaveD.Contains(d) &&
                Math.Abs(reference[r].MidiPitch - detected[d].MidiPitch) == 12 && Math.Abs(reference[r].StartSeconds - detected[d].StartSeconds) <= .15, -1);
            if (d >= 0) { octaves++; octaveD.Add(d); }
        }
        var boundaries = reference.Concat(detected).SelectMany(n => new[] { n.StartSeconds, n.StartSeconds + n.DurationSeconds }).Distinct().Order().ToArray();
        double common = 0, refTime = 0, detTime = 0, groupedTime = 0, exactGroupTime = 0;
        for (int i = 1; i < boundaries.Length; i++)
        {
            double middle = (boundaries[i - 1] + boundaries[i]) / 2, width = boundaries[i] - boundaries[i - 1];
            HashSet<int> At(IReadOnlyList<MusicalNote> notes) => notes.Where(n => n.StartSeconds <= middle && n.StartSeconds + n.DurationSeconds > middle).Select(n => n.MidiPitch).ToHashSet();
            var a = At(reference); var b = At(detected);
            refTime += a.Count * width; detTime += b.Count * width; common += a.Intersect(b).Count() * width;
            if (a.Count >= 2 || b.Count >= 2) { groupedTime += width; if (a.SetEquals(b)) exactGroupTime += width; }
        }
        return new(reference.Count, detected.Count, pairs.Count, reference.Count - pairs.Count, detected.Count - pairs.Count,
            octaves, detected.Count == 0 ? 0 : pairs.Count / (double)detected.Count, reference.Count == 0 ? 0 : pairs.Count / (double)reference.Count,
            pairs.Count == 0 ? null : pairs.Average(p => Math.Abs(reference[p.R].StartSeconds - detected[p.D].StartSeconds)),
            pairs.Count == 0 ? null : pairs.Average(p => Math.Abs(reference[p.R].DurationSeconds - detected[p.D].DurationSeconds)),
            detTime == 0 ? 0 : common / detTime, refTime == 0 ? 0 : common / refTime, groupedTime == 0 ? null : exactGroupTime / groupedTime);
    }

    public static PolyphonicRoundTripResult Validate(MusicalScore score, CancellationToken token = default)
    {
        string source = new SoundScriptOutput().Source(score); token.ThrowIfCancellationRequested();
        var wave = TranscriptionPlayback.Render(source);
        var rendered = new TranscriptionEngine().Transcribe(PcmWaveInput.Decode(wave), new((int)score.TempoMap[0].Bpm, Quantize: false), TranscriptionMode.Polyphonic, token);
        return new(source, wave, Compare(RenderedSchedule(score), rendered.Score.Tracks.SelectMany(t => t.Notes).ToArray()));
    }
    public static async Task<PolyphonicRoundTripResult> ValidateAsync(MusicalScore score, CancellationToken token = default)
    {
        string source = new SoundScriptOutput().Source(score); token.ThrowIfCancellationRequested();
        var wave = TranscriptionPlayback.Render(source);
        var rendered = await new TranscriptionEngine().TranscribeAsync(PcmWaveInput.Decode(wave), new((int)score.TempoMap[0].Bpm, Quantize: false), TranscriptionMode.Polyphonic, token);
        return new(source, wave, Compare(RenderedSchedule(score), rendered.Score.Tracks.SelectMany(t => t.Notes).ToArray()));
    }
    private static MusicalNote[] RenderedSchedule(MusicalScore score) => score.Tracks.SelectMany(t => t.Notes)
        .Select(n => n with { StartSeconds = n.StartBeat * 60 / score.TempoMap[0].Bpm, DurationSeconds = n.DurationBeats * 60 / score.TempoMap[0].Bpm }).ToArray();
}
