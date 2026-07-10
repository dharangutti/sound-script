namespace SoundScript.Wordbank.Models;

public sealed class ManifestDocument
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public int SchemaVersion { get; init; }
    public string DefaultLocale { get; init; } = "en";
    public ManifestLocale[] Locales { get; init; } = [];
}

public sealed class ManifestLocale
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
}
