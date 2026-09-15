using SoundScript.Transcription;
using SoundScript.Wave;
using Xunit;

namespace SoundScript.Tests;

public class TranscriptionModelTests
{
    internal static MusicalScore Score(params MusicalNote[] notes) => new([new(0,120,new(1,"test"))],null,null,
        [new("imported", "melody", 73, notes, [new(2,1)])],[],2);
    internal static MusicalNote Note(int pitch, double start = 0, double duration = 1) => new(pitch,start/2,duration/2,start,duration,80,new(1,"test"));
    [Fact] public void ScoreProducesRealAstSourceAndAudio()
    {
        var source = new SoundScriptOutput().Source(Score(Note(60),Note(64,1.5,.5)));
        Assert.Contains("rest :0.5",source);
        var bytes = WaveRenderer.RenderToBytes(SoundScriptOutput.Parse(source));
        Assert.NotEmpty(PcmWaveInput.Decode(bytes).Samples);
    }
    [Fact] public void OverlappingScoreIsRejected() => Assert.Throws<NotSupportedException>(() => new SoundScriptOutput().Write(Score(Note(60),Note(62,.5))));
    [Fact] public void MalformedWaveIsRejected() => Assert.Throws<InvalidDataException>(() => PcmWaveInput.Decode([1,2,3]));
    [Fact] public void InvalidPcmIsRejected() => Assert.Throws<InvalidDataException>(() => new AnalysisAudio([float.NaN]));
    [Fact] public void OversizedPcmIsRejected() => Assert.Throws<InvalidDataException>(() => new AnalysisAudio(new float[16000*121]));
    [Fact] public void UnsupportedWaveEncodingIsRejected()
    {
        var bytes = WaveRenderer.RenderToBytes(SoundScriptOutput.Parse(new SoundScriptOutput().Source(Score(Note(60)))));
        bytes[20] = 6;
        Assert.Throws<NotSupportedException>(() => PcmWaveInput.Decode(bytes));
    }
    [Fact] public void FractionalTempoIsNotSilentlyRounded() => Assert.Throws<NotSupportedException>(() => new SoundScriptOutput().Write(Score(Note(60)) with { TempoMap = [new(0,120.5,new(1,"test"))] }));
    [Fact] public void DownsamplingRejectsOutOfBandTone()
    {
        var input = Enumerable.Range(0,4800).Select(i => (float)Math.Sin(2*Math.PI*12000*i/48000)).ToArray();
        var output = PcmWaveInput.Normalize(input,48000).Samples;
        Assert.True(output.Skip(100).Take(output.Length-200).Average(x => x*x) < .001);
    }
}
