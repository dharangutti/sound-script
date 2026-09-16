namespace SoundScript.Transcription;

/// <summary>Conservative evidence gate, not an instrument or polyphony classifier.</summary>
public sealed record TranscriptionSuitability(string Status, int DetectedNotes, double VoicedActiveFraction,
    double StableActiveFraction, double ActivePeriodicity, string Reason, double FundamentalSupportedFraction, int UsableNotes)
{
    public bool CanGenerate => Status != "Rejected";
    public static MusicalNote[] SupportedNotes(TranscriptionResult result) => result.Score.Tracks.SelectMany(t=>t.Notes).Where(n =>
    {
        var evidence=result.Observations.Frames.Where(f=>f.Seconds>=n.StartSeconds && f.Seconds<n.StartSeconds+n.DurationSeconds-1e-8 && f.FundamentalShare.HasValue).ToArray();
        return evidence.Length==0 || evidence.Count(f=>f.FundamentalShare>=.08)>=evidence.Length*.8;
    }).ToArray();
    public static MusicalScore ScoreForGeneration(TranscriptionResult result)
    {
        if (!result.Suitability.CanGenerate) throw new InvalidDataException(result.Suitability.Reason);
        var notes=SupportedNotes(result);
        if (result.Score.Tracks.Count!=1) throw new NotSupportedException("Monophonic generation requires one track.");
        if (notes.Length==result.Score.Tracks[0].Notes.Count) return result.Score;
        var rests=new List<MusicalRest>(); double end=0;
        foreach(var note in notes) { if(note.StartBeat>end) rests.Add(new(end,note.StartBeat-end)); end=note.StartBeat+note.DurationBeats; }
        double total=result.Score.DurationSeconds*result.Score.TempoMap[0].Bpm/60;
        if(total>end) rests.Add(new(end,total-end));
        return result.Score with { Tracks=[result.Score.Tracks[0] with { Notes=notes,Rests=rests }] };
    }
    public static TranscriptionSuitability Evaluate(TranscriptionResult result)
    {
        var frames = result.Observations.Frames;
        double threshold = Math.Max(.003, frames.Select(f => f.Rms).DefaultIfEmpty().Max() * .035);
        var active = frames.Where(f => f.Rms >= threshold).ToArray();
        var notes = result.Score.Tracks.SelectMany(t => t.Notes).ToArray();
        var usable = SupportedNotes(result);
        if (result.Extraction is { } extraction)
        {
            bool accepted = usable.Length > 0 && extraction.MelodyCoverage >= .5 && result.Score.DurationSeconds >= .1;
            return new(accepted ? "Experimental" : "Rejected", notes.Length,
                active.Length == 0 ? 0 : active.Count(f => f.Frequency.HasValue) / (double)active.Length,
                extraction.MelodyCoverage, 0,
                accepted ? "Experimental dominant line; accompaniment is omitted and the intended melody is not verified. Compare source and generated playback."
                    : "No defensible dominant line covers at least half of active audio. Competing pitches, octave ambiguity or diffuse energy prevent melody extraction.",
                0, usable.Length);
        }
        // Count supported active frames instead of dividing note duration by recording length:
        // intentional rests must not penalize a clear solo phrase.
        double stable = active.Length == 0 ? 0 : active.Count(f => usable.Any(n =>
            f.Seconds >= n.StartSeconds && f.Seconds < n.StartSeconds + n.DurationSeconds - 1e-8)) / (double)active.Length;
        double voiced = active.Length == 0 ? 0 : active.Count(f => f.Frequency.HasValue) / (double)active.Length;
        var pitched = active.Where(f=>f.Frequency.HasValue && f.FundamentalShare.HasValue).ToArray();
        double fundamental = pitched.Length == 0 ? (voiced == 0 ? 0 : 1) : pitched.Count(f=>f.FundamentalShare >= .08)/(double)pitched.Length;
        string status = usable.Length == 0 || stable < .5 || fundamental < .5 ? "Rejected" : stable < .8 || fundamental < .8 || usable.Length<notes.Length ? "Experimental" : "Supported";
        string reason = usable.Length == 0 ? "No usable stable pitched notes. Silence, percussion or unsupported audio cannot be converted to a melody."
            : fundamental < .5 ? "Most detected pitches lack energy at their fundamental. Overlapping notes can produce a false common subharmonic; a reliable solo melody was not established. Missing-fundamental solo timbres are also unsupported by this conservative check."
            : status == "Rejected" ? "Stable pitches cover less than half of active audio. No reliable monophonic melody was established; percussion, overlapping piano notes and ensembles require other analysis modes."
            : status == "Experimental" ? "Experimental partial melody: substantial active audio has no stable pitch. Missing notes and octave errors are possible; this is not full piano or ensemble transcription."
            : "Stable monophonic pitch evidence. This does not establish source accuracy or rule out overlapping sources.";
        return new(status, notes.Length, voiced, stable, active.Select(f => f.Periodicity).DefaultIfEmpty().Average(), reason, fundamental, usable.Length);
    }
}
