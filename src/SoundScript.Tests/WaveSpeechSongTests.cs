// Verification for speech-only and mixed speech/vocal wave scripts rendered
// without a MIDI step.
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Prosody;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveSpeechSongTests
{
    private static readonly string SpeechOnlyExamplePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../../examples/speech-only-wave.ss"));

    private static ProgramNode LoadExample(string path)
    {
        var source = File.ReadAllText(path);
        return new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
    }

    [Fact]
    public void OnDiskSpeechOnlyExample_RendersWithoutMidiStep()
    {
        Assert.True(File.Exists(SpeechOnlyExamplePath), $"Missing example: {SpeechOnlyExamplePath}");

        var program = LoadExample(SpeechOnlyExamplePath);
        var bytes = WaveRenderer.RenderToBytes(program);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void OnDiskSpeechOnlyExample_IsDeterministic()
    {
        var program = LoadExample(SpeechOnlyExamplePath);
        var first = WaveRenderer.RenderToBytes(program);
        var second = WaveRenderer.RenderToBytes(program);

        Assert.Equal(first, second);
    }

    [Fact]
    public void OnDiskSpeechOnlyExample_ProducesSpeechOverlayEntries()
    {
        var program = LoadExample(SpeechOnlyExamplePath);
        var words = WaveSpeechTimeline.Build(program);

        // At minimum the `speak` phrase is timed; voice { sing } overlay arrives in a later phase.
        Assert.Contains(words, w => w.Text.Contains("prosody tones", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VoiceOnlyScript_RendersAudibleOutput()
    {
        const string source = """
            tempo 120
            voice lead {
                mf
                sing "la la la" C4 q D4 q E4 q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var bytes = WaveRenderer.RenderToBytes(program);

        Assert.NotEmpty(bytes);
    }
}
