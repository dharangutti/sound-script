// V8 — vocal stem mixing and improved prosody export.
using System.Linq;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using SoundScript.Wave.Tts;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveV8Tests
{
    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    [Fact]
    public void Parse_SampleDirective_ProducesSampleNode()
    {
        var program = Parse("""sample "vocals/take.wav" gain=0.8 at=2""");
        var sample = Assert.IsType<SampleNode>(program.Statements[0]);
        Assert.Equal("vocals/take.wav", sample.Path);
        Assert.Equal(0.8, sample.Gain, 3);
        Assert.Equal(2.0, sample.AtBeats);
    }

    [Fact]
    public void Parse_SpeakWithSamplePath_StoresSampleMetadata()
    {
        var program = Parse("""speak "hello world" sample="vocals/hello.wav" gain=0.9 seed=7""");
        var speak = Assert.IsType<SpeakNode>(program.Statements[0]);
        Assert.Equal("hello world", speak.Text);
        Assert.Equal("vocals/hello.wav", speak.SamplePath);
        Assert.Equal(0.9, speak.SampleGain, 3);
    }

    [Fact]
    public void Render_WithSampleStem_MixesRecordingIntoWav()
    {
        var dir = CreateTempScriptDir();
        try
        {
            var stemPath = Path.Combine(dir, "vocal.wav");
            WriteToneWav(stemPath, frequencyHz: 880, durationSeconds: 0.5, amplitude: 0.6);

            var script = Path.Combine(dir, "demo.ssw");
            File.WriteAllText(script,
                """
                tempo 120
                track pad { mf C4 w }
                sample "vocal.wav" gain=0.9 at=0
                """);

            var loaded = ProgramLoader.Load(script);
            var dry = WaveRenderer.RenderToBytes(loaded.Program, new WaveRenderOptions { ScriptDirectory = dir });
            Assert.NotEmpty(dry);

            var withMissing = WaveRenderer.RenderToBytes(loaded.Program, new WaveRenderOptions
            {
                ScriptDirectory = dir,
                SkipMissingSamples = true,
            });
            Assert.NotEmpty(withMissing);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Render_SpeakWithSample_UsesRecordingInsteadOfSyntheticTones()
    {
        var dir = CreateTempScriptDir();
        try
        {
            var stemPath = Path.Combine(dir, "phrase.wav");
            WriteToneWav(stemPath, frequencyHz: 660, durationSeconds: 0.4, amplitude: 0.7);

            var script = Path.Combine(dir, "speak.ssw");
            File.WriteAllText(script,
                """
                tempo 120
                speak "hello world" sample="phrase.wav" seed=7
                """);

            var loaded = ProgramLoader.Load(script);
            var adapted = AstToNoteEventAdapter.Adapt(loaded.Program);
            Assert.Empty(adapted.Tracks.Values.SelectMany(n => n).ToList());
            Assert.Single(adapted.SampleOverlays);

            var bytes = WaveRenderer.RenderToBytes(loaded.Program, new WaveRenderOptions { ScriptDirectory = dir });
            Assert.NotEmpty(bytes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TtsDirectoryMapper_ResolvesSlugFilenames()
    {
        var dir = CreateTempScriptDir();
        try
        {
            WriteToneWav(Path.Combine(dir, "hello-world.wav"), 440, 0.3, 0.5);
            var program = Parse("""speak "Hello world" seed=1""");
            var adapted = AstToNoteEventAdapter.Adapt(program);
            var overlays = TtsDirectoryMapper.BuildOverlays(adapted.SpeakTimings, dir);
            Assert.Single(overlays);
            Assert.EndsWith("hello-world.wav", overlays[0].RelativePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WavReader_RoundTripsWriterOutput()
    {
        var original = new float[4410];
        for (var i = 0; i < original.Length; i++)
            original[i] = (float)(0.25 * Math.Sin(i / 10.0));

        var path = Path.Combine(CreateTempScriptDir(), "roundtrip.wav");
        try
        {
            WavWriter.Write(path, original);
            var read = WavReader.ReadMono(path);
            Assert.Equal(original.Length, read.Length);
            Assert.True(read.Zip(original, (a, b) => Math.Abs(a - b)).Max() < 0.02);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void Example_WaveVocalStem_RendersWithBundledStem()
    {
        var exampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples"));
        var script = Path.Combine(exampleDir, "wave-vocal-stem.ssw");
        var loaded = ProgramLoader.Load(script);
        var bytes = WaveRenderer.RenderToBytes(loaded.Program, new WaveRenderOptions { ScriptDirectory = exampleDir });
        Assert.NotEmpty(bytes);
    }

    private static string CreateTempScriptDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "soundscript-v8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteToneWav(string path, double frequencyHz, double durationSeconds, double amplitude)
    {
        var sampleCount = (int)Math.Round(WavWriter.SampleRate * durationSeconds);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)WavWriter.SampleRate;
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * frequencyHz * t));
        }

        WavWriter.Write(path, samples);
    }
}
