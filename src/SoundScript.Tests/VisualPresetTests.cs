using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Playground;
using SoundScript.Visual;
using SoundScript.Wave.Io;
using Xunit;

namespace SoundScript.Tests;

public class VisualPresetTests
{
    [Theory]
    [InlineData("visual-temporal", 12, 5)]
    [InlineData("visual-motion", 6, 2)]
    [InlineData("visual-story", 7, 3)]
    [InlineData("visual-overlays", 8, 3)]
    public void Presets_CompileAndRenderThePublishedSource(string key, int seconds, int visuals)
    {
        var preset = VisualPresetCatalog.All.Single(p => p.Key == key);
        var metadata = PlaygroundPresetCatalog.TryGet(key);
        Assert.NotNull(metadata);
        Assert.Equal(PlaygroundOutputRail.AudioVisual, metadata.OutputRail);
        Assert.Equal(preset.ExampleFile, metadata.ExampleFile);
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples", preset.ExampleFile));
        Assert.Equal(File.ReadAllText(path), preset.Source);
        var program = new SoundScript.Parser.Parser(new Tokenizer(preset.Source).Tokenize()).Parse();
        var timeline = VisualInterpreter.Interpret(program);
        Assert.Equal(TimeSpan.FromSeconds(seconds), timeline.Duration);
        Assert.Equal(visuals, timeline.Visuals.Count);
        Assert.Single(timeline.AudioSyncPoints);
        Assert.Empty(timeline.StateAt(timeline.Duration).Elements);

        var scene = TemporalVisualSceneBuilder.Build(timeline.StateAt(TimeSpan.FromSeconds(1)));
        Assert.NotEmpty(scene.Primitives);
        var plan = TemporalVideoExportPlanBuilder.Build(timeline);
        Assert.Equal(seconds * 30, plan.Samples.Count);
        using var wav = new MemoryStream(TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration));
        var audio = WavReader.ReadMono(wav);
        Assert.Equal(seconds * WavWriter.SampleRate, audio.Length);
        Assert.Contains(audio, sample => Math.Abs(sample) > 0.001f);

        if (key == "visual-story")
            Assert.Empty(timeline.StateAt(TimeSpan.FromSeconds(2.5)).Elements);
        if (key == "visual-overlays")
            Assert.Equal(3, timeline.StateAt(TimeSpan.FromSeconds(5)).Elements.Count);
        if (key == "visual-motion")
            Assert.Contains(timeline.StateAt(TimeSpan.FromSeconds(3)).Elements[0].Properties,
                property => property.Property == "x" && property.Value == 0.5m);
    }
}
