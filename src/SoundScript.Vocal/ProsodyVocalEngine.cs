using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Io;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Vocal;

/// <summary>
/// Built-in deterministic fallback — renders <c>speak</c> through SoundScript.Wave
/// (synthetic prosody tones). Always available; no external binaries.
/// </summary>
public sealed class ProsodyVocalEngine : IVocalEngine
{
    public string Name => "prosody";

    public void Synthesize(string text, string outputWavPath, VocalEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var source = $$"""
            tempo 120
            speak "{{escaped}}" seed={{options.Seed}}
            """;
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

        using var stream = new MemoryStream();
        WaveRenderer.RenderTo(program, stream);
        stream.Position = 0;
        var samples = WavReader.ReadMono(stream);
        samples = VocalStemNormalizer.Normalize(samples, options.OutputGain);

        if (VocalStemNormalizer.Peak(samples) <= 1e-6)
        {
            throw new InvalidOperationException(
                "Prosody engine produced a silent stem — check speak text and seed.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputWavPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        WavWriter.Write(outputWavPath, samples);
    }
}
