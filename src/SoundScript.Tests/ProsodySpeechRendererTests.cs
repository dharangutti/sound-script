using SoundScript.Wave.Io;
using SoundScript.Wave.Prosody;
using Xunit;

namespace SoundScript.Tests;

public class ProsodySpeechRendererTests
{
    [Fact]
    public void RenderStem_ProducesAudibleSyntheticSpeech()
    {
        var samples = ProsodySpeechRenderer.RenderStem("hello world", seed: 7);
        Assert.NotEmpty(samples);

        var peak = samples.Max(s => Math.Abs(s));
        Assert.True(peak > 0.05, "Stem should contain audible synthetic phoneme energy.");
    }

    [Fact]
    public void GenerateForVocalStem_ExtendsPhonemeDurations()
    {
        var inline = ProsodyToneGenerator.Generate("hello", "default", 7);
        var stem = ProsodyToneGenerator.GenerateForVocalStem("hello", 7);

        var inlineSound = inline.Where(t => !t.IsRest).Sum(t => t.DurationBeats);
        var stemSound = stem.Where(t => !t.IsRest).Sum(t => t.DurationBeats);
        Assert.True(stemSound > inlineSound * 1.5);
    }
}
