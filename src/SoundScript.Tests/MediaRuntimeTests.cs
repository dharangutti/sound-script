using SoundScript.Media;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public class MediaRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MediaTimeMatchesPcmEventsAcrossTempoBoundary(bool ramp)
    {
        var source = (ramp ? "tempo 120 -> 180 over 1 bars" : "tempo 120") + """

            track notes { C4 q D4 q E4 q F4 q G4 q }
            sync audio
            visual "moving" for 4s { animate x 0 -> 1 over 4s }
            visual "boundary" for 1s at 1s
            """;
        var program = new SoundScript.Parser.Parser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
        var adapted = SoundScript.Wave.Adapter.AstToNoteEventAdapter.Adapt(program);
        var map = TemporalAudioRenderer.BuildTempoMap(program);
        var media = SoundScriptEngine.Compile(source).CompileMedia();
        var notes = adapted.Tracks.Single().Value;
        for (var beat = 0; beat < notes.Count; beat++)
        {
            var time = map.BeatsToMilliseconds(0, beat) / 1000;
            Assert.Equal(time, notes[beat].StartTimeSeconds, 6);
            var scene = media.SceneAt(TimeSpan.FromSeconds(time));
            var state = media.Timeline.StateAtAudioBeat(beat, map);
            Assert.InRange(Math.Abs(state.Time.TotalSeconds - scene.TimeSeconds), 0, 0.000001);
            Assert.Equal(state.Elements.Select(e => e.Name), scene.Primitives.Select(p => p.Name));
        }
        foreach (var time in new[] { 0d, 1, 1.5, 2, 3.999, 4 })
        {
            var scene = media.SceneAt(TimeSpan.FromSeconds(time));
            if (time < 4) Assert.Equal((decimal)time / 4 * 1280, scene.Primitives[0].Left + scene.Primitives[0].Width / 2);
            else Assert.Empty(scene.Primitives);
        }
        var raw = SoundScriptEngine.Compile(source).RenderWave();
        Assert.Equal(raw.AsSpan(44).ToArray(), media.RenderAudio().AsSpan(44, raw.Length - 44).ToArray());
    }

    [Fact]
    public void MediaPreservesSandboxAndReportsInvalidInputs()
    {
        var root = Path.Combine(Path.GetTempPath(), "soundscript-v14-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var entry = Path.Combine(root, "main.ss");
            File.WriteAllText(entry, "sample \"missing.wav\" gain=1 at=0");
            var media = SoundScriptEngine.CompileFile(entry, new SoundScript.Core.AllowedPathRoot(root)).CompileMedia();
            Assert.Throws<FileNotFoundException>(() => media.RenderAudio());
            File.WriteAllText(entry, "sample \"../outside.wav\" gain=1 at=0");
            var escape = SoundScriptEngine.CompileFile(entry, new SoundScript.Core.AllowedPathRoot(root)).CompileMedia();
            Assert.Throws<InvalidOperationException>(() => escape.RenderAudio());
            Assert.Throws<NotSupportedException>(() => SoundScriptEngine.Compile("import \"other.ss\""));
            Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.Compile("visual \"bad\" for -1s").CompileMedia());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

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
