using SoundScript.Transcription;
using SoundScript.Cli;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public sealed class TranscriptionAcceptanceTests
{
    [Fact] public void SuccessfulMonophonicFixturesKeepTheirNotesAndSource()
    {
        foreach (var fixture in TranscriptionFixture.All)
        {
            var r = new MonophonicTranscriber().Transcribe(fixture.Audio());
            Assert.Equal("Supported", r.Suitability.Status);
            Assert.Equal(1, MusicalComparison.Compare(fixture.Truth,r.Score.Tracks[0].Notes).PitchAccuracy);
            string path = Path.Combine(AppContext.BaseDirectory,"Golden","transcription",fixture.Name+".ss");
            Assert.Equal(File.ReadAllText(path).Replace("\r\n","\n"),new SoundScriptOutput().Source(r.Score).Replace("\r\n","\n"));
        }
    }
    [Fact] public void SilenceAndZeroNoteObservationsCannotGenerate()
    {
        var r = new MonophonicTranscriber().Transcribe(new(new float[16000]));
        Assert.False(r.Suitability.CanGenerate);
        Assert.Equal(0,r.Suitability.DetectedNotes);
        Assert.Throws<InvalidDataException>(()=>MusicalComparison.Validate(r.Score));
        var empty = new MonophonicTranscriber().Interpret(new([],0,[]),new());
        Assert.False(empty.Suitability.CanGenerate);
    }
    [Theory] [InlineData("tempo 120\ntrack t { rest :4 }")] [InlineData("tempo 120\ntrack t { }")]
    public void SilentEditedSourceCannotPlayOrExport(string source)
        => Assert.Throws<InvalidDataException>(()=>TranscriptionPlayback.Render(source));

    [Theory] [InlineData("drums")] [InlineData("piano")] [InlineData("ensemble")]
    public void UnsupportedSyntheticMaterialIsNotFalseSuccess(string kind)
    {
        // Deterministic original signals, not excerpts of user recordings.
        // Nonharmonic overlapping piano tones, noisy percussion, and their mixture.
        var samples = new float[32000]; var random = new Random(13013);
        for (int i=0;i<samples.Length;i++)
        {
            double t=i/16000.0, local=t%.25;
            double piano=(Math.Sin(2*Math.PI*261.626*t)+Math.Sin(2*Math.PI*329.628*t)+Math.Sin(2*Math.PI*391.995*t))*.17*Math.Exp(-local*3);
            double drums=(random.NextDouble()*2-1)*.6*Math.Exp(-local*15);
            samples[i]=(float)(kind=="drums"?drums:kind=="piano"?piano:piano*.65+drums*.65);
        }
        var result=new MonophonicTranscriber().Transcribe(new(samples));
        Assert.False(result.Suitability.CanGenerate,$"{kind}: {result.Suitability}");
    }
    [Theory] [InlineData(.49,"Rejected")] [InlineData(.60,"Experimental")] [InlineData(.90,"Supported")]
    public void PartialEvidenceHasExplicitDisposition(double fraction,string expected)
    {
        var frames=Enumerable.Range(0,100).Select(i=>new PitchFrame(i*.01,i<fraction*100?440:null,i<fraction*100?.99:0,.2)).ToArray();
        var result=new MonophonicTranscriber().Interpret(new(frames,1,[]),new());
        Assert.Equal(expected,result.Suitability.Status);
    }
    [Fact] public void OversizedPcmReportsDurationAndExplicitExcerptWorks()
    {
        var samples=new float[121*16000];
        var phrase=TranscriptionFixture.All[0].Audio().Samples;
        Array.Copy(phrase,0,samples,10*16000,phrase.Length);
        // Writer accepts a file; keep the acceptance input temporary.
        var path=Path.GetTempFileName();
        try
        {
            WavWriter.Write(path,samples,16000); var bytes=File.ReadAllBytes(path);
            var error=Assert.Throws<InvalidDataException>(()=>PcmWaveInput.Decode(bytes));
            Assert.Contains("121.000",error.Message); Assert.Contains("120-second",error.Message);
            var trimmed=PcmWaveInput.Decode(bytes,new(10,phrase.Length/16000.0));
            Assert.Equal(phrase.Length,trimmed.Samples.Length);
            Assert.Equal("Supported",new MonophonicTranscriber().Transcribe(trimmed).Suitability.Status);
        }
        finally { File.Delete(path); }
    }
    [Theory] [InlineData(-1,1)] [InlineData(0,121)] [InlineData(10,0)] [InlineData(119,2)] [InlineData(double.NaN,1)]
    public void InvalidExcerptIsRejected(double start,double duration)
        => Assert.Throws<InvalidDataException>(()=>new MediaExcerpt(start,duration).Validate(120));
    [Theory] [InlineData("--start","NaN")] [InlineData("--duration","121")] [InlineData("--duration","0")]
    public void InvalidCliExcerptIsRejected(string option,string value)
        => Assert.Throws<CliUsageException>(()=>CliArguments.Parse(["transcribe","x.wav","--out","x.ss",option,value]));
    [Fact] public void CliRejectionWritesDiagnosticsWithoutSourceOrPreview()
    {
        string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string input=Path.Combine(root,"silent.wav"),source=Path.Combine(root,"score.ss"),preview=Path.Combine(root,"preview.wav"),report=Path.Combine(root,"report.json");
            WavWriter.Write(input,new float[16000],16000);
            Assert.Throws<InvalidDataException>(()=>CommandHandlers.Execute(CliArguments.Parse(["transcribe",input,"--out",source,"--preview",preview,"--report",report])));
            Assert.False(File.Exists(source)); Assert.False(File.Exists(preview));
            using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(report));
            Assert.False(json.RootElement.GetProperty("Generated").GetBoolean());
            Assert.Equal("Rejected",json.RootElement.GetProperty("Transcription").GetProperty("Suitability").GetProperty("Status").GetString());
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact] public void ExperimentalOutputOmitsUnsupportedPitchCandidates()
    {
        var frames=Enumerable.Range(0,100).Select(i=>new PitchFrame(i*.01,i<70?440:330,.99,.2,i<70?.9:0)).ToArray();
        var result=new MonophonicTranscriber().Interpret(new(frames,1,[]),new());
        Assert.Equal(2,result.Suitability.DetectedNotes);
        Assert.Equal(1,result.Suitability.UsableNotes);
        Assert.Equal("Experimental",result.Suitability.Status);
        var score=TranscriptionSuitability.ScoreForGeneration(result);
        Assert.Single(score.Tracks[0].Notes);
        Assert.Equal(69,score.Tracks[0].Notes[0].MidiPitch);
        Assert.NotEmpty(TranscriptionPlayback.Render(new SoundScriptOutput().Source(score)));
    }
    [FfmpegFact] public async Task OversizedMp3HasMetadataAndSupportsExplicitTrim()
    {
        string root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string input=Path.Combine(root,"long.mp3"),output=Path.Combine(root,"trim.ss");
            SoundScript.Media.FfmpegWebmExporter.RunMediaProcess(FfmpegFactAttribute.Executable,
                ["-nostdin","-y","-f","lavfi","-i","sine=frequency=440:sample_rate=16000:duration=121","-c:a","libmp3lame",input]);
            var adapter=new DesktopMediaInput(FfmpegFactAttribute.Executable);
            var info=await adapter.InspectAsync(input);
            Assert.True(info.DurationSeconds>120); Assert.Equal(16000,info.SampleRate);
            await Assert.ThrowsAsync<InvalidDataException>(()=>adapter.DecodeAsync(input));
            var trimmed=await adapter.DecodeAsync(input,new MediaExcerpt(30,2));
            Assert.Equal(32000,trimmed.Samples.Length);
            Assert.Equal("Supported",new MonophonicTranscriber().Transcribe(trimmed).Suitability.Status);
            Assert.Equal(0,CommandHandlers.Execute(CliArguments.Parse(["transcribe",input,"--start","30","--duration","2","--out",output,"--ffmpeg",FfmpegFactAttribute.Executable])));
            Assert.True(File.Exists(output));
        }
        finally { Directory.Delete(root,true); }
    }
}
