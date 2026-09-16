using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Transcription;
using Xunit;

namespace SoundScript.Tests;

public sealed class MixedTranscriptionTests
{
    private static MusicalNote N(int pitch,double start,double duration)=>new(pitch,start,duration,start*2,duration*2,80,new(1,"Authored role fixture"));
    public static AnalysisAudio Audio(string kind)
    {
        if(kind is "dense" or "noise")return PolyphonicFixture.All.Single(f=>f.Name==(kind=="dense"?"dense-ensemble":"noise")).Audio();
        MusicalNote[] notes = kind switch {
            "melody-bass" => [N(43,.2,.7),N(48,1,.7),N(76,.2,.4),N(79,.7,.4),N(74,1.2,.4)],
            _ => [N(43,.2,1.5),N(60,.2,1.5),N(64,.2,1.5),N(76,.2,.4),N(79,.7,.4),N(74,1.2,.4)]
        };
        var audio = new PolyphonicFixture(kind,notes,2).Audio();
        if(kind=="drums")
        {
            var random=new Random(311);
            return new(audio.Samples.Select((s,i)=>(float)(s*.8+.15*(random.NextDouble()*2-1)*Math.Exp(-(i/16000.0%.5)*40))).ToArray());
        }
        return audio;
    }
    [Theory] [InlineData("melody-bass")] [InlineData("melody-chords")] [InlineData("drums")] [InlineData("small-ensemble")]
    public void SharedEngineProducesRolesWithTimingAndNoStemClaim(string kind)
    {
        var result=new TranscriptionEngine().Transcribe(Audio(kind),new(120,0,false),TranscriptionMode.Mixed);
        Assert.Equal("Experimental",result.Suitability.Status);
        Assert.Contains(result.Score.Tracks,t=>t.Role=="melody");Assert.Contains(result.Score.Tracks,t=>t.Role=="bass");
        if(kind!="melody-bass")Assert.Contains(result.Score.Tracks,t=>t.Role=="harmony");
        Assert.Contains("no isolated audio stems",result.Mixed!.SeparationKind);
        Assert.All(result.Score.Tracks.Where(t=>t.Role=="melody").SelectMany(t=>t.Notes),n=>Assert.Contains(n.MidiPitch,new[]{74,76,79}));
        Assert.All(result.Score.Tracks.Where(t=>t.Role=="bass").SelectMany(t=>t.Notes),n=>Assert.Contains(n.MidiPitch,new[]{43,48}));
        var source=new SoundScriptOutput().Source(result.Score);
        Assert.Contains(PcmWaveInput.Decode(TranscriptionPlayback.Render(source)).Samples,s=>Math.Abs(s)>.003);
        Assert.Contains(result.Diagnostics,d=>d.Code=="unsupported-mixed-material");
    }
    [Theory] [InlineData("noise")] [InlineData("dense")]
    public void UnsupportedMixturesReject(string kind)
    {
        var result=new TranscriptionEngine().Transcribe(Audio(kind),mode:TranscriptionMode.Mixed);
        Assert.False(result.Suitability.CanGenerate);
        Assert.Throws<InvalidDataException>(()=>TranscriptionSuitability.ScoreForGeneration(result));
    }
    [Fact] public async Task SelectionPreservesTimingAndIsDeterministic()
    {
        var engine=new TranscriptionEngine();var audio=Audio("melody-chords");
        var all=engine.Transcribe(audio,new(120,0,false),TranscriptionMode.Mixed);
        var options=new TranscriptionOptions(120,0,false){Roles=["melody","bass"]};
        var selected=engine.Transcribe(audio,options,TranscriptionMode.Mixed);
        Assert.DoesNotContain(selected.Score.Tracks,t=>t.Role=="harmony");
        string Json(object value)=>JsonSerializer.Serialize(value,AnalysisJsonOutput.Options);
        Assert.Equal(Json(all.Score.Tracks.Where(t=>t.Role!="harmony")),Json(selected.Score.Tracks));
        Assert.Equal(Json(selected),Json(await engine.TranscribeAsync(audio,options,TranscriptionMode.Mixed)));
        Assert.Equal(Json(selected),Json(engine.Transcribe(audio,options,TranscriptionMode.Mixed)));
        using var cts=new CancellationTokenSource();cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>engine.TranscribeAsync(audio,options,TranscriptionMode.Mixed,cts.Token));
    }
    [Theory] [InlineData("melody,melody")] [InlineData("drums")] [InlineData("melody,")]
    public void InvalidRolesFailClearly(string roles)=>Assert.Throws<CliUsageException>(()=>CliArguments.Parse(["transcribe","input.wav","--mode","mixed","--roles",roles,"--out","out.ss"]));
    [Fact] public void RolesCannotBeAppliedToOtherModes()=>Assert.Throws<CliUsageException>(()=>CliArguments.Parse(["transcribe","input.wav","--mode","polyphonic","--roles","bass","--out","out.ss"]));
    [Fact] public void EmptyRoleSelectionRejects()=>Assert.Throws<ArgumentException>(()=>new TranscriptionEngine().Transcribe(Audio("melody-bass"),new(){Roles=[]},TranscriptionMode.Mixed));
}
