namespace SoundScript.Transcription;

public sealed record PercussionMetrics(double OnsetPrecision, double OnsetRecall, double MatchedClassAgreement,
    double MeanOnsetErrorSeconds, int MissedHits, int ExtraHits);
public sealed record PercussionValidation(string Source, byte[] PreviewWave, PercussionMetrics ScoreToRenderedAudio);

public static class PercussionComparison
{
    public static PercussionMetrics Compare(IReadOnlyList<PercussionHit> expected, IReadOnlyList<PercussionHit> observed)
    {
        var used = new HashSet<int>(); int matches = 0, classes = 0; double error = 0;
        foreach (var hit in expected.OrderBy(h => h.StartSeconds))
        {
            int index = Enumerable.Range(0, observed.Count).Where(i => !used.Contains(i) && Math.Abs(hit.StartSeconds - observed[i].StartSeconds) <= .05)
                .OrderBy(i => Math.Abs(hit.StartSeconds - observed[i].StartSeconds)).DefaultIfEmpty(-1).First();
            if (index < 0) continue;
            used.Add(index); matches++; error += Math.Abs(hit.StartSeconds - observed[index].StartSeconds);
            if (hit.Sound == observed[index].Sound) classes++;
        }
        return new(observed.Count == 0 ? 0 : matches / (double)observed.Count, expected.Count == 0 ? 0 : matches / (double)expected.Count,
            matches == 0 ? 0 : classes / (double)matches, matches == 0 ? 0 : error / matches, expected.Count - matches, observed.Count - matches);
    }

    public static PercussionValidation Validate(MusicalScore score, CancellationToken token = default)
        => ValidateCore(score, false, token).GetAwaiter().GetResult();
    public static Task<PercussionValidation> ValidateAsync(MusicalScore score, CancellationToken token = default)
        => ValidateCore(score, true, token);

    private static async Task<PercussionValidation> ValidateCore(MusicalScore score, bool yield, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (score.Tracks.Any(t => t.Notes.Count != 0 || t.Percussion == null)) throw new NotSupportedException("Rhythm comparison requires percussion-only tracks.");
        string source = new SoundScriptOutput().Source(score);
        if (yield) await Task.Delay(1, token);
        byte[] wave = TranscriptionPlayback.Render(source);
        token.ThrowIfCancellationRequested();
        var audio = PcmWaveInput.Decode(wave);
        var options = new TranscriptionOptions((int)score.TempoMap[0].Bpm, Quantize: false);
        var observed = yield ? await new PercussionTranscriber().TranscribeAsync(audio, options, token) : new PercussionTranscriber().Transcribe(audio, options, token);
        var expected = score.Tracks.SelectMany(t => t.Percussion!).Select(h => h with { StartSeconds = h.StartBeat * 60 / score.TempoMap[0].Bpm }).ToArray();
        return new(source, wave, Compare(expected, observed.Percussion!.Hits));
    }
}
