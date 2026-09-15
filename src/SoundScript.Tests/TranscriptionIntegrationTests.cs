using System.Diagnostics;
using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Media;
using SoundScript.Transcription;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public sealed class TranscriptionIntegrationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"soundscript-transcription-tests-"+Guid.NewGuid().ToString("N"));
    public TranscriptionIntegrationTests()=>Directory.CreateDirectory(root);
    public void Dispose()=>Directory.Delete(root,true);
    private string PathFor(string name)=>Path.Combine(root,name);
    private string Fixture()
    {
        string path=PathFor("input.wav"); WavWriter.Write(path,TranscriptionFixture.All[0].Audio().Samples,16000); return path;
    }
    [Fact] public void CliCreatesParseableSourcePreviewAndReport()
    {
        var args=CliArguments.Parse(["transcribe",Fixture(),"--output",PathFor("output.ss"),"--report",PathFor("report.json"),"--preview",PathFor("preview.wav"),"--tempo","120","--instrument","flute"]);
        Assert.Equal(0,CommandHandlers.Execute(args));
        _=SoundScriptOutput.Parse(File.ReadAllText(PathFor("output.ss")));
        Assert.NotEmpty(PcmWaveInput.Decode(File.ReadAllBytes(PathFor("preview.wav"))).Samples);
        using var report=JsonDocument.Parse(File.ReadAllText(PathFor("report.json")));
        Assert.Equal(1,report.RootElement.GetProperty("RoundTrip").GetProperty("PitchAccuracy").GetDouble());
    }
    [Theory] [InlineData("--tempo","NaN")] [InlineData("--tempo","0")] [InlineData("--instrument","not-an-instrument")] [InlineData("--preview","bad.mp3")]
    public void CliRejectsInvalidOptions(string key,string value) => Assert.Throws<CliUsageException>(()=>CliArguments.Parse(["transcribe","input.wav","--out","output.ss",key,value]));
    [Fact] public void CliRefusesAliasingReportAndInput()
    {
        string input=Fixture(); var original=File.ReadAllBytes(input);
        Assert.Throws<CliUsageException>(()=>CommandHandlers.Execute(CliArguments.Parse(["transcribe",input,"--out",PathFor("output.ss"),"--report",input])));
        Assert.Equal(original,File.ReadAllBytes(input)); Assert.False(File.Exists(PathFor("output.ss")));
    }
    [Fact] public async Task UnsupportedMediaIsRejected()
    { string path=PathFor("input.txt");File.WriteAllText(path,"invalid");await Assert.ThrowsAsync<NotSupportedException>(()=>new DesktopMediaInput().DecodeAsync(path)); }
    [Fact] public void TruncatedWaveIsRejected()
    { var bytes=File.ReadAllBytes(Fixture());Assert.Throws<InvalidDataException>(()=>PcmWaveInput.Decode(bytes[..^1])); }
    [Fact] public void ConfidenceEqualsMeasuredPeriodicity()
    {
        var result=new MonophonicTranscriber().Transcribe(TranscriptionFixture.All[0].Audio());
        Assert.Equal(result.Score.Tracks[0].Notes.Average(n=>n.PitchEvidence.Confidence),result.PitchConfidence,12);
        Assert.InRange(result.PitchConfidence,0,1);
    }
    [Fact] public void CommittedWaveFixturesReproduceMeasuredPitchCounts()
    {
        foreach(var fixture in TranscriptionFixture.All)
        {
            var path=Path.Combine(AppContext.BaseDirectory,"Golden","transcription",fixture.Name+".wav");
            var result=new MonophonicTranscriber().Transcribe(PcmWaveInput.Decode(File.ReadAllBytes(path)));
            var metric=MusicalComparison.Compare(fixture.Truth,result.Score.Tracks[0].Notes);
            Assert.Equal(1,metric.PitchAccuracy); Assert.Equal(0,metric.ExtraNotes);
        }
    }
    [FfmpegFact] public async Task RealMp3AndMp4WithVideoExtractAndTranscribe()
    {
        string input=Fixture();
        foreach(var extension in new[]{"mp3","mp4"})
        {
            string target=PathFor("media."+extension);
            string[] args=extension=="mp3" ? ["-nostdin","-y","-i",input,target] :
                ["-nostdin","-y","-f","lavfi","-i","color=c=black:s=32x32:r=10","-i",input,"-shortest","-c:v","mpeg4","-c:a","aac",target];
            FfmpegWebmExporter.RunMediaProcess(FfmpegFactAttribute.Executable,args);
            var audio=await new DesktopMediaInput(FfmpegFactAttribute.Executable).DecodeAsync(target);
            var result=new MonophonicTranscriber().Transcribe(audio);
            var metrics=MusicalComparison.Compare(TranscriptionFixture.All[0].Truth,result.Score.Tracks[0].Notes);
            Assert.Equal(1,metrics.PitchAccuracy); Assert.Equal(0,metrics.ExtraNotes);
        }
    }
    [FfmpegFact] public async Task VideoWithoutAudioFailsClearly()
    {
        string input=PathFor("silent-video.mp4");
        FfmpegWebmExporter.RunMediaProcess(FfmpegFactAttribute.Executable,["-nostdin","-y","-f","lavfi","-i","color=c=black:s=32x32:r=10","-t","0.2","-c:v","mpeg4",input]);
        await Assert.ThrowsAsync<ExportException>(()=>new DesktopMediaInput(FfmpegFactAttribute.Executable).DecodeAsync(input));
    }
}

public sealed class FfmpegFactAttribute : FactAttribute
{
    public static string Executable => Environment.GetEnvironmentVariable("SOUNDSCRIPT_FFMPEG")??"ffmpeg";
    public FfmpegFactAttribute()
    {
        try
        {
            using var p=Process.Start(new ProcessStartInfo(Executable,"-version"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})!;
            p.StandardOutput.ReadToEnd();p.StandardError.ReadToEnd();
            if(!p.WaitForExit(5000) || p.ExitCode!=0) Skip="Real FFmpeg integration requires an installed FFmpeg executable.";
        }
        catch(System.ComponentModel.Win32Exception){Skip="Real FFmpeg integration requires an installed FFmpeg executable.";}
    }
}
