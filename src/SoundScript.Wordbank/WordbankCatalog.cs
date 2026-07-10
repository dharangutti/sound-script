using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

/// <summary>
/// Global access point for embedded wordbank locale packs.
/// </summary>
public static class WordbankCatalog
{
    private static readonly Lazy<ManifestDocument> Manifest = new(LoadManifest);
    private static readonly Lazy<IReadOnlyDictionary<string, LocalePack>> Locales = new(LoadAllLocales);
    private static string _activeCode = "en";

    static WordbankCatalog()
    {
        _activeCode = Manifest.Value.DefaultLocale;
    }

    /// <summary>Code of the currently active locale (default: manifest defaultLocale).</summary>
    public static string ActiveLocaleCode => _activeCode;

    /// <summary>The currently active locale pack.</summary>
    public static LocalePack Active => GetLocale(_activeCode);

    /// <summary>Alias for <see cref="Active"/>.</summary>
    public static LocalePack Default => Active;

    /// <summary>All embedded locale codes.</summary>
    public static IReadOnlyCollection<string> AvailableLocales =>
        Locales.Value.Keys.OrderBy(code => code, StringComparer.Ordinal).ToArray();

    /// <summary>Gets an embedded locale pack by code.</summary>
    public static LocalePack GetLocale(string code)
    {
        if (!Locales.Value.TryGetValue(code, out var pack))
        {
            throw new ArgumentException(
                $"Unknown wordbank locale '{code}'. Available: {string.Join(", ", AvailableLocales)}");
        }

        return pack;
    }

    /// <summary>Switches the active locale for subsequent engine calls.</summary>
    public static bool TrySetActive(string code, out string? error)
    {
        if (!Locales.Value.ContainsKey(code))
        {
            error = $"Unknown wordbank locale '{code}'. Available: {string.Join(", ", AvailableLocales)}";
            return false;
        }

        _activeCode = code;
        error = null;
        return true;
    }

    /// <summary>Resets the active locale to the manifest default.</summary>
    public static void ResetActive() => _activeCode = Manifest.Value.DefaultLocale;

    /// <summary>Loads a locale pack from a directory on disk (for tests or custom packs).</summary>
    public static LocalePack LoadFromDirectory(string localeDirectory) =>
        LocalePack.FromDirectory(localeDirectory);

    private static ManifestDocument LoadManifest()
    {
        using var stream = typeof(WordbankCatalog).Assembly
            .GetManifestResourceStream("SoundScript.Wordbank.Data.manifest.json")
            ?? throw new InvalidOperationException("Embedded manifest.json not found.");

        return LocalePack.Deserialize<ManifestDocument>(stream);
    }

    private static IReadOnlyDictionary<string, LocalePack> LoadAllLocales()
    {
        var assembly = typeof(WordbankCatalog).Assembly;
        var locales = new Dictionary<string, LocalePack>(StringComparer.Ordinal);

        foreach (var locale in Manifest.Value.Locales)
            locales[locale.Code] = LocalePack.FromEmbeddedResources(assembly, locale.Code);

        return locales;
    }
}
