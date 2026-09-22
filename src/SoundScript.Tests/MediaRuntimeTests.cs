using SoundScript.Media;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public class MediaRuntimeTests
{
    [Fact]
    public void FacadePreservesIntervalsOverlapWaitAndInterpolation()
    {
        var media = SoundScriptEngine.Compile("""
            visual "first" for 2s { animate x 0 -> 1 over 2s }
            wait 1s
            visual "last" for 1s
            visual "overlap" for 1s at 1s
            """).CompileMedia();
        Assert.Throws<ArgumentOutOfRangeException>(() => media.SceneAt(TimeSpan.FromTicks(-1)));
        Assert.Single(media.SceneAt(TimeSpan.Zero).Primitives);
        var middle = media.SceneAt(TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { "first", "overlap" }, middle.Primitives.Select(p => p.Name));
        Assert.Equal(640m, middle.Primitives[0].Left + middle.Primitives[0].Width / 2);
        Assert.Empty(media.SceneAt(TimeSpan.FromSeconds(2)).Primitives);
        Assert.Single(media.SceneAt(TimeSpan.FromSeconds(3.999)).Primitives);
        Assert.Empty(media.SceneAt(TimeSpan.FromSeconds(4)).Primitives);
        Assert.Empty(media.SceneAt(TimeSpan.FromSeconds(10)).Primitives);
        Assert.Equal(TimeSpan.FromSeconds(4), media.Duration);
        using var stream = new MemoryStream(media.RenderAudio());
        Assert.Equal(4 * WavWriter.SampleRate, WavReader.ReadMono(stream).Length);
    }

    [Fact]
    public void AudioIsPreservedAndDefensivelyCopiedWithoutVisuals()
    {
        var compilation = SoundScriptEngine.Compile("tempo 120\nC4 q D4 q");
        var media = compilation.CompileMedia();
        var expected = compilation.RenderWave();
        Assert.Equal(expected, media.RenderAudio());
        Assert.True(media.Duration > TimeSpan.Zero);
        Assert.Empty(media.SceneAt(media.Duration).Primitives);
        var changed = media.RenderAudio();
        changed[0] = 0;
        Assert.Equal(expected, media.RenderAudio());
    }

    [Fact]
    public void EmptyProgramAndIndependentCompilationsAreDeterministic()
    {
        var a = SoundScriptEngine.Compile("").CompileMedia();
        var b = SoundScriptEngine.Compile("").CompileMedia();
        Assert.Empty(a.SceneAt(TimeSpan.Zero).Primitives);
        Assert.Equal(a.RenderAudio(), b.RenderAudio());
        Assert.Equal(a.Duration, b.Duration);
    }
}
