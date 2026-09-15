using System.Text.Json;
using SoundScript.Transcription;
using SoundScript.Wave.Io;
using Xunit;
using Xunit.Abstractions;

namespace SoundScript.Tests;

public class TranscriptionAnalysisTests(ITestOutputHelper output)
{
    [Theory] [InlineData(110,45)] [InlineData(261.6256,60)] [InlineData(440,69)] [InlineData(880,81)]
    public void YinDetectsPitch(double frequency,int midi)
    {
        var samples=Enumerable.Range(0,640).Select(i=>(float)(.5*Math.Sin(2*Math.PI*frequency*i/16000))).ToArray();
        var detected=MonophonicAnalyzer.DetectPitch(samples);
        Assert.NotNull(detected.Frequency); Assert.Equal(midi,MonophonicAnalyzer.FrequencyToMidi(detected.Frequency!.Value));
        Assert.InRange(detected.Periodicity,.98,1);
    }
    [Theory] [InlineData(96)] [InlineData(120)] [InlineData(150)]
    public void TempoUsesOnsets(int tempo) => Assert.Equal(tempo,MonophonicTranscriber.EstimateTempo(Enumerable.Range(0,8).Select(i=>i*60.0/tempo).ToArray()));
    [Fact] public void QuantizationPreservesIrregularTiming()
    { Assert.Equal(1,MonophonicTranscriber.Quantize(1.03)); Assert.Equal(1.11,MonophonicTranscriber.Quantize(1.11)); }
    [Fact] public void SilenceAndShortInputHaveDiagnostics()
    {
        var result=new MonophonicTranscriber().Transcribe(new(new float[400]));
        Assert.Empty(result.Score.Tracks[0].Notes); Assert.Equal(0,result.PitchConfidence);
        Assert.Contains(result.Diagnostics,d=>d.Code=="silence"); Assert.Contains(result.Diagnostics,d=>d.Code=="short-input");
    }
    [Fact] public void ClippingAndNoiseAreReported()
    {
        var random=new Random(81); var pcm=Enumerable.Range(0,16000).Select(_=>(float)(random.NextDouble()*2-1)).ToArray();
        for(int i=0;i<100;i++) pcm[i]=1;
        var result=new MonophonicTranscriber().Transcribe(new(pcm));
        Assert.Contains(result.Diagnostics,d=>d.Code=="clipping"); Assert.Contains(result.Diagnostics,d=>d.Code=="weak-periodicity");
    }
    [Fact] public void FiveOriginalFixturesMeetNoteAndTimingTargetsAndRender()
    {
        var reports=new List<object>();
        var destination=Environment.GetEnvironmentVariable("SOUNDSCRIPT_TRANSCRIPTION_FIXTURES");
        if(destination!=null) Directory.CreateDirectory(destination);
        foreach(var fixture in TranscriptionFixture.All)
        {
            var transcriber=new MonophonicTranscriber();
            var result=transcriber.Transcribe(fixture.Audio());
            var again=transcriber.Transcribe(fixture.Audio());
            Assert.Equal(new AnalysisJsonOutput().Write(result),new AnalysisJsonOutput().Write(again));
            var metrics=MusicalComparison.Compare(fixture.Truth,result.Score.Tracks[0].Notes,fixture.Tempo,result.Score.TempoMap[0].Bpm);
            var roundTrip=MusicalComparison.Validate(result.Score);
            output.WriteLine(fixture.Name+": "+JsonSerializer.Serialize(new { metrics,roundTrip.ScoreToRenderedAudio }));
            reports.Add(new {fixture.Name,fixture.Tempo,fixture.Timbre,fixture.Truth,metrics,roundTrip.ScoreToRenderedAudio,result.PitchConfidence,result.TimingGridFit,EstimatedTempo=result.Score.TempoMap[0].Bpm});
            if(destination!=null)
            {
                WavWriter.Write(Path.Combine(destination,fixture.Name+".wav"),fixture.Audio().Samples,16000);
                File.WriteAllText(Path.Combine(destination,fixture.Name+".ss"),roundTrip.Source);
                File.WriteAllBytes(Path.Combine(destination,fixture.Name+"-preview.wav"),roundTrip.PreviewWave);
            }
            Assert.Equal(0,metrics.MissedNotes); Assert.Equal(0,metrics.ExtraNotes); Assert.Equal(1,metrics.PitchAccuracy);
            Assert.InRange(metrics.OnsetMaeSeconds!.Value,0,.05); Assert.InRange(metrics.DurationMaeSeconds!.Value,0,.06);
            Assert.Equal(metrics.RepeatedReferenceNotes,metrics.MatchedRepeatedNotes);
            Assert.Equal(1,roundTrip.ScoreToRenderedAudio.PitchAccuracy);
        }
        if(destination!=null) File.WriteAllText(Path.Combine(destination,"measurements.json"),JsonSerializer.Serialize(reports,AnalysisJsonOutput.Options));
    }
    [Fact] public void MusicalMetricsCountMissingExtraAndWrongPitch()
    {
        var a=TranscriptionModelTests.Note(60); var b=TranscriptionModelTests.Note(62,2);
        var m=MusicalComparison.Compare([a,b],[a with{MidiPitch=61},b with{StartSeconds=4}]);
        Assert.Equal(1,m.MissedNotes); Assert.Equal(1,m.ExtraNotes); Assert.Equal(0,m.PitchAccuracy);
    }
    [Fact] public void CancellationIsObserved()
    { using var c=new CancellationTokenSource();c.Cancel();Assert.Throws<OperationCanceledException>(()=>new MonophonicTranscriber().Transcribe(new(new float[16000]),cancellationToken:c.Token)); }
    [Fact] public async Task CooperativeBrowserAnalysisMatchesSynchronousAnalysis()
    {
        var audio = TranscriptionFixture.All[0].Audio();
        var analyzer = new MonophonicAnalyzer();
        Assert.Equal(JsonSerializer.Serialize(analyzer.Analyze(audio)), JsonSerializer.Serialize(await analyzer.AnalyzeAsync(audio)));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => analyzer.AnalyzeAsync(audio, cancel.Token));
    }
    [Fact] public void LegatoPitchChangesAndAmplitudeRepeatedAttacksAreDetected()
    {
        var samples = new float[32000];
        for (int i = 0; i < samples.Length; i++)
        {
            double frequency = i < 16000 ? 261.625565 : 329.627557;
            // Repeated C4 at .5 s, with a fresh attack after a low-energy valley.
            double amplitude = i is >= 7400 and < 8000 ? .02 : .4;
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequency * i / 16000));
        }
        var notes = new MonophonicTranscriber().Transcribe(new(samples),new(120,Quantize:false)).Score.Tracks[0].Notes;
        Assert.Equal(new[]{60,60,64}, notes.Select(n => n.MidiPitch));
        Assert.InRange(notes[1].StartSeconds,.48,.52);
        Assert.InRange(notes[2].StartSeconds,.97,1.03);
    }
    [Fact] public void ModerateNoisePreservesCleanMelodyNotes()
    {
        var fixture = TranscriptionFixture.All[0]; var samples = fixture.Audio().Samples; var random = new Random(2026);
        for (int i=0;i<samples.Length;i++) samples[i] += (float)((random.NextDouble()-.5)*.035);
        var result = new MonophonicTranscriber().Transcribe(new(samples));
        var metrics = MusicalComparison.Compare(fixture.Truth,result.Score.Tracks[0].Notes);
        Assert.Equal(1,metrics.PitchAccuracy); Assert.Equal(0,metrics.ExtraNotes);
    }
    [Fact] public void SimultaneousSourcesRemainAnExplicitUnsupportedAssumption()
    {
        var fixture = TranscriptionFixture.All[0]; var samples = fixture.Audio().Samples;
        for (int i=0;i<samples.Length;i++) samples[i]=(float)(.65*samples[i]+.25*Math.Sin(2*Math.PI*196*i/16000));
        var result = new MonophonicTranscriber().Transcribe(new(samples));
        var metric = MusicalComparison.Compare(fixture.Truth,result.Score.Tracks[0].Notes);
        output.WriteLine("Two-source experiment: "+JsonSerializer.Serialize(metric));
        Assert.Contains(result.Diagnostics,d=>d.Code=="monophonic-assumption");
        Assert.Null(result.Score.Key); Assert.Null(result.Score.Meter); Assert.Null(result.Score.Tracks[0].Chords);
    }
}
