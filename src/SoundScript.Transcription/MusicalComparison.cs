using SoundScript.Wave;

namespace SoundScript.Transcription;

public sealed record ComparisonMetrics(int ReferenceNotes, int DetectedNotes, int MatchedNotes, int MissedNotes, int ExtraNotes,
    double PitchAccuracy, double? OnsetMaeSeconds, double? DurationMaeSeconds, double? MelodicContourAccuracy,
    double? TempoErrorBpm, double? RestIntersectionOverUnion, int RepeatedReferenceNotes, int MatchedRepeatedNotes);
public sealed record RoundTripResult(string Source, byte[] PreviewWave, ComparisonMetrics ScoreToRenderedAudio);

public static class MusicalComparison
{
    /// <summary>One-to-one temporal matching within 150 ms. Pitch is scored independently of matching.</summary>
    public static ComparisonMetrics Compare(IReadOnlyList<MusicalNote> reference, IReadOnlyList<MusicalNote> detected,
        double? referenceTempo = null, double? detectedTempo = null)
    {
        var pairs=new List<(int R,int D)>(); var used=new HashSet<int>();
        for (int r=0;r<reference.Count;r++)
        {
            int best=-1; double distance=.15000001;
            for (int d=0;d<detected.Count;d++)
            {
                double delta=Math.Abs(reference[r].StartSeconds-detected[d].StartSeconds);
                if (!used.Contains(d) && delta<distance) { distance=delta; best=d; }
            }
            if (best>=0) { used.Add(best); pairs.Add((r,best)); }
        }
        double? onset=pairs.Count==0?null:pairs.Average(p=>Math.Abs(reference[p.R].StartSeconds-detected[p.D].StartSeconds));
        double? duration=pairs.Count==0?null:pairs.Average(p=>Math.Abs(reference[p.R].DurationSeconds-detected[p.D].DurationSeconds));
        var contour=new List<bool>();
        for (int i=1;i<pairs.Count;i++)
            if (pairs[i].R==pairs[i-1].R+1 && pairs[i].D==pairs[i-1].D+1)
                contour.Add(Math.Sign(reference[pairs[i].R].MidiPitch-reference[pairs[i-1].R].MidiPitch)==Math.Sign(detected[pairs[i].D].MidiPitch-detected[pairs[i-1].D].MidiPitch));
        int repeats=0, matchedRepeats=0;
        for (int r=1;r<reference.Count;r++) if (reference[r].MidiPitch==reference[r-1].MidiPitch)
        {
            repeats++;
            if (pairs.Any(p=>p.R==r && detected[p.D].MidiPitch==reference[r].MidiPitch) && pairs.Any(p=>p.R==r-1 && detected[p.D].MidiPitch==reference[r-1].MidiPitch)) matchedRepeats++;
        }
        // Integrate silence intersection/union exactly across all note boundaries over the union duration.
        var boundaries=reference.Concat(detected).SelectMany(n=>new[]{n.StartSeconds,n.StartSeconds+n.DurationSeconds}).Append(0).Distinct().Order().ToArray();
        double intersection=0,union=0;
        for (int i=1;i<boundaries.Length;i++)
        {
            double mid=(boundaries[i]+boundaries[i-1])/2, width=boundaries[i]-boundaries[i-1];
            bool a=!reference.Any(n=>n.StartSeconds<=mid && n.StartSeconds+n.DurationSeconds>mid);
            bool b=!detected.Any(n=>n.StartSeconds<=mid && n.StartSeconds+n.DurationSeconds>mid);
            if (a&&b) intersection+=width; if(a||b) union+=width;
        }
        return new(reference.Count,detected.Count,pairs.Count,reference.Count-pairs.Count,detected.Count-pairs.Count,
            reference.Count==0?0:pairs.Count(p=>reference[p.R].MidiPitch==detected[p.D].MidiPitch)/(double)reference.Count,
            onset,duration,contour.Count==0?null:contour.Count(x=>x)/(double)contour.Count,
            referenceTempo.HasValue&&detectedTempo.HasValue?Math.Abs(referenceTempo.Value-detectedTempo.Value):null,
            union==0?null:intersection/union,repeats,matchedRepeats);
    }
    public static RoundTripResult Validate(MusicalScore score, CancellationToken cancellationToken = default)
    {
        var writer=new SoundScriptOutput(); string source=writer.Source(score);
        cancellationToken.ThrowIfCancellationRequested();
        var preview=WaveRenderer.RenderToBytes(SoundScriptOutput.Parse(source));
        var rendered=new MonophonicTranscriber().Transcribe(PcmWaveInput.Decode(preview),new(Tempo:(int)score.TempoMap[0].Bpm,Quantize:false),cancellationToken);
        double secondsPerBeat=60/score.TempoMap[0].Bpm;
        var expected=score.Tracks.SelectMany(t=>t.Notes).Select(n=>n with { StartSeconds=n.StartBeat*secondsPerBeat,DurationSeconds=n.DurationBeats*secondsPerBeat }).OrderBy(n=>n.StartSeconds).ToArray();
        return new(source,preview,Compare(expected,rendered.Score.Tracks.SelectMany(t=>t.Notes).ToArray()));
    }
}
