namespace SoundScript.Wordbank.Models;

public sealed class LocaleManifestDocument
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public LocaleFiles Files { get; init; } = new();
}

public sealed class LocaleFiles
{
    public string FunctionWords { get; init; } = "";
    public string StressPrefixes { get; init; } = "";
    public string WordProsody { get; init; } = "";
    public string GraphemeRules { get; init; } = "";
    public string LegalOnsets { get; init; } = "";
    public string PhonemeCompose { get; init; } = "";
    public string PhonemeWave { get; init; } = "";
    public string WordEntries { get; init; } = "";
}
