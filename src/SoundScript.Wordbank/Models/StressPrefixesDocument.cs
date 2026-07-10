namespace SoundScript.Wordbank.Models;

public sealed class StressPrefixesDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public string[] Prefixes { get; init; } = [];
}
