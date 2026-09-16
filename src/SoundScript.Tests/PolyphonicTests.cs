using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Transcription;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using Xunit;
using Xunit.Abstractions;

namespace SoundScript.Tests;

public sealed class PolyphonicTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases => PolyphonicFixture.All.Where(f => f.Timbre == "piano").Select(f => new object[] { f.Name });
    [Theory] [MemberData(nameof(Cases))]
    public void KnownPerformancesPreserveSimultaneousPitches(string name)
    {
        var fixture = PolyphonicFixture.All.Single(f=>f.Name==name);
        var result = new TranscriptionEngine().Transcribe(fixture.Audio(), new(120,0, false), TranscriptionMode.Polyphonic);
        var notes = result.Score.Tracks.SelectMany(t=>t.Notes).ToArray();
        var metrics = PolyphonicComparison.Compare(fixture.Truth, notes);
        output.WriteLine(JsonSerializer.Serialize(new { result.Suitability, metrics, Notes=notes.Select(n=>new {n.MidiPitch,n.StartSeconds,n.DurationSeconds}) }));
        Assert.Equal("Experimental",result.Suitability.Status);
        Assert.Equal(1,metrics.NotePrecision); Assert.Equal(1,metrics.NoteRecall);
        Assert.InRange(metrics.OnsetMaeSeconds!.Value,0,.1);
        Assert.InRange(metrics.DurationMaeSeconds!.Value,0,.2);
        if (name is not ("monophonic" or "harmonics" or "arpeggio")) Assert.True(result.Polyphony!.MaximumSimultaneousNotes>=2);
        var source = new SoundScriptOutput().Source(TranscriptionSuitability.ScoreForGeneration(result));
        _=SoundScriptOutput.Parse(source);
        Assert.Contains(PcmWaveInput.Decode(TranscriptionPlayback.Render(source)).Samples,s=>Math.Abs(s)>.003);
        if (Environment.GetEnvironmentVariable("SOUNDSCRIPT_POLYPHONIC_ARTIFACTS") is { } root)
        {
            Directory.CreateDirectory(root);
            WavWriter.Write(Path.Combine(root,name+".wav"),fixture.Audio().Samples,16000);
            var roundTrip=PolyphonicComparison.Validate(result.Score);
            File.WriteAllText(Path.Combine(root,name+".ss"),roundTrip.Source);
            File.WriteAllBytes(Path.Combine(root,name+"-preview.wav"),roundTrip.PreviewWave);
            var melody=new TranscriptionEngine().Transcribe(fixture.Audio(),new(120,0,false),TranscriptionMode.ExtractMelody);
            File.WriteAllText(Path.Combine(root,name+".json"),JsonSerializer.Serialize(new {
                fixture.Name, fixture.Tempo, fixture.Duration, GroundTruth=fixture.Truth,
                Polyphonic=metrics, ExtractMelody=PolyphonicComparison.Compare(fixture.Truth,melody.Score.Tracks.SelectMany(t=>t.Notes).ToArray()),
                ExtractMelodySuitability=melody.Suitability.Status, result.Polyphony, RoundTrip=roundTrip.ScoreToRenderedAudio
            },AnalysisJsonOutput.Options));
        }
    }
    [Theory] [InlineData("dense")] [InlineData("dense-ensemble")] [InlineData("missing-fundamental")] [InlineData("noise")] [InlineData("drums")] [InlineData("silence")]
    public void UnsupportedEvidenceRejects(string name)
    {
        var fixture=PolyphonicFixture.All.Single(f=>f.Name==name);
        var result=new TranscriptionEngine().Transcribe(fixture.Audio(),mode:TranscriptionMode.Polyphonic);
        output.WriteLine(JsonSerializer.Serialize(result.Suitability));
        Assert.Equal("Rejected",result.Suitability.Status);
        Assert.Throws<InvalidDataException>(()=>TranscriptionSuitability.ScoreForGeneration(result));
    }
    [Fact] public async Task IdenticalSyncAsyncAndRepeatedResultsAndCancellation()
    {
        var audio=PolyphonicFixture.All[1].Audio(); var engine=new TranscriptionEngine();
        string Json(TranscriptionResult r)=>JsonSerializer.Serialize(r,AnalysisJsonOutput.Options);
        string first=Json(engine.Transcribe(audio,mode:TranscriptionMode.Polyphonic));
        Assert.Equal(first,Json(engine.Transcribe(audio,mode:TranscriptionMode.Polyphonic)));
        Assert.Equal(first,Json(await engine.TranscribeAsync(audio,mode:TranscriptionMode.Polyphonic)));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>engine.Transcribe(audio,mode:TranscriptionMode.Polyphonic,cancellationToken:cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>engine.TranscribeAsync(audio,mode:TranscriptionMode.Polyphonic,cancellationToken:cancel.Token));
    }
    [Theory] [InlineData(86)] [InlineData(101)] [InlineData(179)] [InlineData(null)]
    public void SourcePreservesEveryVoiceAtArbitraryTempos(int? tempo)
    {
        var result=new TranscriptionEngine().Transcribe(PolyphonicFixture.All.Single(f=>f.Name=="overlap").Audio(),new(tempo,0),TranscriptionMode.Polyphonic);
        var source=new SoundScriptOutput().Source(result.Score);
        var adapted=AstToNoteEventAdapter.Adapt(SoundScriptOutput.Parse(source));
        Assert.Equal(result.Score.Tracks.Count,adapted.Tracks.Count);
        Assert.Equal(result.Score.Tracks.Sum(t=>t.Notes.Count),adapted.Tracks.Sum(t=>t.Value.Count));
        var expected=result.Score.Tracks.SelectMany(t=>t.Notes).OrderBy(n=>n.MidiPitch).ToArray();
        var actual=adapted.Tracks.SelectMany(t=>t.Value).OrderBy(n=>n.FrequencyHz).ToArray();
        for(int i=0;i<expected.Length;i++)
        {
            Assert.Equal(expected[i].MidiPitch,MonophonicAnalyzer.FrequencyToMidi(actual[i].FrequencyHz));
            Assert.InRange(Math.Abs(expected[i].StartBeat*60/result.Score.TempoMap[0].Bpm-actual[i].StartTimeSeconds),0,.00001);
            Assert.InRange(Math.Abs(expected[i].DurationBeats*60/result.Score.TempoMap[0].Bpm-actual[i].DurationSeconds),0,.00001);
        }
        Assert.Contains(PcmWaveInput.Decode(TranscriptionPlayback.Render(source)).Samples,s=>Math.Abs(s)>.003);
    }
    [Fact] public void PolyphonicMetricsMatchByPitchAndTimeWithoutReusingNotes()
    {
        var truth=PolyphonicFixture.All[1].Truth;
        var shuffled=new[]{truth[2],truth[0],truth[1],truth[0]};
        var metrics=PolyphonicComparison.Compare(truth,shuffled);
        Assert.Equal(1,metrics.NoteRecall);Assert.Equal(.75,metrics.NotePrecision);Assert.Equal(1,metrics.ExtraNotes);
        var octave=PolyphonicComparison.Compare(truth,[truth[0] with { MidiPitch=truth[0].MidiPitch+12 }]);
        Assert.Equal(1,octave.OctaveErrors);Assert.Equal(0,octave.MatchedNotes);
    }
    [Theory] [InlineData("major-triad",true)] [InlineData("noise",false)]
    public void CliWritesMatchingPolyphonicSourceOrRejectionReport(string name,bool accepted)
    {
        string root=Path.Combine(Path.GetTempPath(),"soundscript-polyphonic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string input=Path.Combine(root,"input.wav"),source=Path.Combine(root,"score.ss"),preview=Path.Combine(root,"preview.wav"),report=Path.Combine(root,"report.json");
            WavWriter.Write(input,PolyphonicFixture.All.Single(f=>f.Name==name).Audio().Samples,16000);
            var args=CliArguments.Parse(["transcribe",input,"--mode","polyphonic","--tempo","120","--out",source,"--preview",preview,"--report",report]);
            if(accepted) Assert.Equal(0,CommandHandlers.Execute(args));else Assert.Throws<InvalidDataException>(()=>CommandHandlers.Execute(args));
            using var json=JsonDocument.Parse(File.ReadAllText(report));Assert.Equal(accepted,json.RootElement.GetProperty("Generated").GetBoolean());
            var core=new TranscriptionEngine().Transcribe(PcmWaveInput.Decode(File.ReadAllBytes(input)),new(120,0),TranscriptionMode.Polyphonic);
            if(accepted)
            {
                Assert.Equal(new SoundScriptOutput().Source(core.Score),File.ReadAllText(source));
                Assert.Contains(PcmWaveInput.Decode(File.ReadAllBytes(preview)).Samples,s=>Math.Abs(s)>.003);
                Assert.True(json.RootElement.GetProperty("Transcription").GetProperty("Polyphony").GetProperty("MaximumSimultaneousNotes").GetInt32()>=3);
            }
            else {Assert.False(File.Exists(source));Assert.False(File.Exists(preview));}
        }
        finally {Directory.Delete(root,true);}
    }
}
