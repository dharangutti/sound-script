namespace SoundScript.Wordbank.Models;

public sealed class SyllabificationDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public string[] VowelLetters { get; init; } = [];
    public string[] AccentedVowels { get; init; } = [];
    public bool TreatYAsVowel { get; init; }
    public bool SilentTerminalE { get; init; }
    public bool ConsonantLeNucleus { get; init; }
    public int MaxOnsetClusterLength { get; init; } = 3;
    public string[] NucleusDigraphs { get; init; } = [];
}
