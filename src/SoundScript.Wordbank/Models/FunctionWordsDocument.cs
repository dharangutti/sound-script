namespace SoundScript.Wordbank.Models;

public sealed class FunctionWordsDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public Dictionary<string, string[]> Categories { get; init; } = new(StringComparer.Ordinal);
}
