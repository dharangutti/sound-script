namespace SoundScript.Wordbank.Models;

public sealed class WordEntriesDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public WordEntry[] Entries { get; init; } = [];
}

public sealed class WordEntry
{
    public string Word { get; init; } = "";
    public string[]? Syllables { get; init; }
    public string[]? Stress { get; init; }
    public string? Category { get; init; }
}
