namespace SoundScript.Wordbank.Models;

public sealed class GraphemeRulesDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public GraphemeRule[] Digraphs { get; init; } = [];
    public SingleLetterRule[] SingleLetters { get; init; } = [];
}

public sealed class GraphemeRule
{
    public string Grapheme { get; init; } = "";
    public string[] Phonemes { get; init; } = [];
}

public sealed class SingleLetterRule
{
    public string Letter { get; init; } = "";
    public string[] Phonemes { get; init; } = [];
}
