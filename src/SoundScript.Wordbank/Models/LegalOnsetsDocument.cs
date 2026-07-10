namespace SoundScript.Wordbank.Models;

public sealed class LegalOnsetsDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public string[] Onsets { get; init; } = [];
}
