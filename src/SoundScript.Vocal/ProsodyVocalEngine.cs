using SoundScript.Parser;
using SoundScript.Wave;
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
        var source = $$"""speak "{{escaped}}" seed={{options.Seed}}""";
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        WaveRenderer.Render(program, outputWavPath);
    }
}
