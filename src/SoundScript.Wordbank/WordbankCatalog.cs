namespace SoundScript.Wordbank;

/// <summary>
/// Global access point for the embedded default English wordbank locale.
/// </summary>
public static class WordbankCatalog
{
    private static readonly Lazy<LocalePack> DefaultLocale = new(() =>
        LocalePack.FromEmbeddedResources(typeof(WordbankCatalog).Assembly, "en"));

    /// <summary>The default embedded locale pack (English).</summary>
    public static LocalePack Default => DefaultLocale.Value;

    /// <summary>Loads a locale pack from a directory on disk (for tests or custom packs).</summary>
    public static LocalePack LoadFromDirectory(string localeDirectory) =>
        LocalePack.FromDirectory(localeDirectory);
}
