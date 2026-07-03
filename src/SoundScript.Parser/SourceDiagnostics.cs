using SoundScript.Core;

namespace SoundScript.Parser;

/// <summary>Lightweight source diagnostics for browser and CLI hosts.</summary>
public static class SourceDiagnostics
{
    public static bool ContainsImport(string source)
    {
        var tokens = new Tokenizer(source).Tokenize();
        return tokens.Any(token => token.Type == TokenType.Import);
    }
}
