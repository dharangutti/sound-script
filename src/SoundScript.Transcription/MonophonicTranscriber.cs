namespace SoundScript.Transcription;

public sealed class MonophonicTranscriber(IMusicalAnalyzer? analyzer = null)
{
    private readonly IMusicalAnalyzer analyzer = analyzer ?? new MonophonicAnalyzer();
    public TranscriptionResult Transcribe(AnalysisAudio audio, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
        => Interpret(analyzer.Analyze(audio,cancellationToken), options ?? new());

    public TranscriptionResult Interpret(MusicalObservations observations, TranscriptionOptions options)
    {
        if (options.Tempo is < 20 or > 300 || options.Instrument is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(options));
        var frames=observations.Frames;
        if (!double.IsFinite(observations.DurationSeconds) || observations.DurationSeconds < 0
            || frames.Any(f => !double.IsFinite(f.Seconds) || f.Seconds < 0 || f.Seconds > observations.DurationSeconds)
            || frames.Zip(frames.Skip(1), (a,b) => b.Seconds <= a.Seconds).Any(x => x))
            throw new InvalidDataException("Observation times must be finite, increasing and within the recording.");
        var pitches=frames.Select(f => f.Frequency.HasValue ? MonophonicAnalyzer.FrequencyToMidi(f.Frequency.Value) : -1).ToArray();
        // Three-frame median rejects isolated pitch glitches, but keeps genuine silence.
        var smoothed=(int[])pitches.Clone();
        for (int i=1;i<pitches.Length-1;i++)
            if (pitches[i]>=0 && pitches[i-1]>=0 && pitches[i+1]>=0)
                smoothed[i]=new[]{pitches[i-1],pitches[i],pitches[i+1]}.Order().ElementAt(1);
        var segments=new List<(int Pitch,double Start,double End,double Confidence,double Rms)>();
        int begin=0;
        for (int i=1;i<=smoothed.Length;i++)
        {
            bool attack=i<smoothed.Length && frames[i].Seconds-frames[begin].Seconds>=.08-1e-8 && frames[i].Rms > frames[i-1].Rms*1.8 && frames[i].Rms>.02;
            if (i<smoothed.Length && smoothed[i]==smoothed[begin] && !attack) continue;
            double segmentEnd=i<frames.Count?frames[i].Seconds:observations.DurationSeconds;
            if (smoothed[begin]>=0 && segmentEnd-frames[begin].Seconds>=.06-1e-8)
            {
                var range=frames.Skip(begin).Take(i-begin).ToArray();
                segments.Add((smoothed[begin],frames[begin].Seconds,segmentEnd,range.Average(f=>f.SpectralShare ?? f.Periodicity),range.Average(f=>f.Rms)));
            }
            begin=i;
        }
        var boundaries=segments.SelectMany(n => new[]{n.Start,n.End}).ToArray();
        int tempo=options.Tempo ?? EstimateTempo(segments.Select(n=>n.Start).ToArray());
        double fit=boundaries.Length==0?0:boundaries.Average(t=>Math.Max(0,1-Math.Abs(t*tempo/60*4-Math.Round(t*tempo/60*4))/.5));
        var notes=new List<MusicalNote>();
        double previousEnd=0;
        foreach (var n in segments)
        {
            double start=n.Start*tempo/60, end=n.End*tempo/60;
            if (options.Quantize)
            {
                double quantizedStart=Quantize(start), quantizedEnd=Quantize(end);
                if (quantizedEnd>quantizedStart) { start=quantizedStart; end=quantizedEnd; }
            }
            start=Math.Max(previousEnd,start); end=Math.Max(start+.01,end);
            notes.Add(new(n.Pitch,n.Start,n.End-n.Start,start,end-start,Math.Clamp((int)Math.Round(100*Math.Sqrt(n.Rms)),35,110),new(n.Confidence,
                frames.Any(f => f.SpectralShare.HasValue) ? "Mean selected spectral peak energy / total window energy; not periodicity or a correctness probability"
                    : "Mean YIN periodicity (1-CMND), not a calibrated probability")));
            previousEnd=end;
        }
        var rests=new List<MusicalRest>(); double cursor=0;
        foreach (var note in notes) { if (note.StartBeat>cursor+1e-7) rests.Add(new(cursor,note.StartBeat-cursor)); cursor=note.StartBeat+note.DurationBeats; }
        double total=observations.DurationSeconds*tempo/60;
        if (options.Quantize) total=Quantize(total);
        if (total>cursor+1e-7) rests.Add(new(cursor,total-cursor));
        var diagnostics=observations.Diagnostics.ToList();
        if (options.Tempo==null) diagnostics.Add(new("uncertain-tempo","Tempo is an onset-grid hypothesis (half/double-time ambiguity); supply a known tempo when available."));
        if (fit<.7) diagnostics.Add(new("irregular-timing","Weak fixed-tempo grid fit; off-grid boundaries are preserved. Tempo changes are not inferred."));
        if (notes.Count==0 && !diagnostics.Any(d=>d.Code=="silence")) diagnostics.Add(new("no-notes","No stable pitched segment of at least 60 ms was found."));
        var score=new MusicalScore([new(0,tempo,new(options.Tempo.HasValue?1:fit,options.Tempo.HasValue?"User supplied":"Onset grid hypothesis; confidence field measures grid fit only"))],null,null,
            [new("transcribed","melody",options.Instrument,notes,rests)],[],observations.DurationSeconds);
        return new(score,observations,notes.Count==0?0:notes.Average(n=>n.PitchEvidence.Confidence),fit,diagnostics);
    }
    public static double Quantize(double beat)
    {
        double nearest=Math.Round(beat*4)/4;
        return Math.Round(Math.Abs(nearest-beat)<=.065?nearest:beat,6);
    }
    public static int EstimateTempo(IReadOnlyList<double> onsets)
    {
        if (onsets.Count<3) return 120;
        int best=120; double bestCost=double.MaxValue;
        for (int bpm=60;bpm<=180;bpm++)
        {
            double cost=0;
            for (int i=1;i<onsets.Count;i++)
            {
                double beats=(onsets[i]-onsets[i-1])*bpm/60;
                double grid=Math.Abs(beats*4-Math.Round(beats*4));
                double whole=Math.Abs(beats-Math.Round(beats));
                cost+=grid*grid+.025*whole;
            }
            cost/=onsets.Count-1;
            cost+=.00001*Math.Abs(bpm-120);
            if (cost<bestCost) { best=bpm; bestCost=cost; }
        }
        return best;
    }
}
