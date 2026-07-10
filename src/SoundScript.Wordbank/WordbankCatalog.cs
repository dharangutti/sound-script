using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

/// <summary>
/// Global access point for embedded or disk-loaded wordbank locale packs.
/// </summary>
public static class WordbankCatalog
{
    private static readonly Lazy<ManifestDocument> EmbeddedManifest = new(LoadEmbeddedManifest);
    private static readonly Lazy<IReadOnlyDictionary<string, LocalePack>> EmbeddedLocales = new(LoadEmbeddedLocales);

    private static readonly object RegistryLock = new();
    private static IReadOnlyDictionary<string, LocalePack>? _externalLocales;
    private static ManifestDocument? _externalManifest;
    private static string? _loadedRoot;
    private static int _generation;
    private static string _activeCode = "en";

    [ThreadStatic]
    private static string? _threadActiveCode;

    static WordbankCatalog()
    {
        _activeCode = EmbeddedManifest.Value.DefaultLocale;
    }

    /// <summary>Increments when the catalog source or locale registry changes.</summary>
    public static int Generation => _generation;

    /// <summary>True when locale packs were loaded from disk via <see cref="TryLoadFromRoot"/>.</summary>
    public static bool IsExternal => _externalLocales is not null;

    /// <summary>Root directory last passed to <see cref="TryLoadFromRoot"/>, if any.</summary>
    public static string? LoadedRoot => _loadedRoot;

    private static string ActiveCode =>
        _threadActiveCode ?? _activeCode;

    private static ManifestDocument CurrentManifest =>
        _externalManifest ?? EmbeddedManifest.Value;

    private static IReadOnlyDictionary<string, LocalePack> LocaleRegistry =>
        _externalLocales ?? EmbeddedLocales.Value;

    /// <summary>Code of the currently active locale (default: manifest defaultLocale).</summary>
    public static string ActiveLocaleCode => ActiveCode;

    /// <summary>The currently active locale pack.</summary>
    public static LocalePack Active => GetLocale(ActiveCode);

    /// <summary>Alias for <see cref="Active"/>.</summary>
    public static LocalePack Default => Active;

    /// <summary>Package version from the active manifest.</summary>
    public static string PackageVersion => CurrentManifest.Version;

    /// <summary>All locale codes from the active catalog source.</summary>
    public static IReadOnlyCollection<string> AvailableLocales =>
        LocaleRegistry.Keys.OrderBy(code => code, StringComparer.Ordinal).ToArray();

    /// <summary>Gets a locale pack by code from the active catalog source.</summary>
    public static LocalePack GetLocale(string code)
    {
        if (!LocaleRegistry.TryGetValue(code, out var pack))
        {
            throw new ArgumentException(
                $"Unknown wordbank locale '{code}'. Available: {string.Join(", ", AvailableLocales)}");
        }

        return pack;
    }

    /// <summary>Switches the active locale for subsequent engine calls.</summary>
    public static bool TrySetActive(string code, out string? error)
    {
        if (!LocaleRegistry.ContainsKey(code))
        {
            error = $"Unknown wordbank locale '{code}'. Available: {string.Join(", ", AvailableLocales)}";
            return false;
        }

        _threadActiveCode = code;
        _activeCode = code;
        error = null;
        return true;
    }

    /// <summary>Resets the active locale to the manifest default.</summary>
    public static void ResetActive()
    {
        _threadActiveCode = null;
        _activeCode = CurrentManifest.DefaultLocale;
    }

    /// <summary>
    /// Loads all locale packs from a wordbank repository root (contains
    /// <c>manifest.json</c> and locale directories referenced by the manifest).
    /// Replaces embedded packs until <see cref="ResetToEmbedded"/> is called.
    /// </summary>
    public static bool TryLoadFromRoot(string wordbankRoot, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(wordbankRoot))
        {
            error = "Wordbank root path is empty.";
            return false;
        }

        var root = Path.GetFullPath(wordbankRoot);
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            error = $"Wordbank manifest not found: {manifestPath}";
            return false;
        }

        try
        {
            var manifest = LocalePack.DeserializeFile<ManifestDocument>(manifestPath);
            var locales = new Dictionary<string, LocalePack>(StringComparer.Ordinal);

            foreach (var locale in manifest.Locales)
            {
                var localeDirectory = ResolveLocaleDirectory(root, locale);
                if (!Directory.Exists(localeDirectory))
                {
                    error = $"Locale directory not found: {localeDirectory}";
                    return false;
                }

                locales[locale.Code] = LocalePack.FromDirectory(localeDirectory);
            }

            lock (RegistryLock)
            {
                _externalManifest = manifest;
                _externalLocales = locales;
                _loadedRoot = root;
                _generation++;
                _threadActiveCode = null;
                _activeCode = manifest.DefaultLocale;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Restores the embedded catalog and clears any disk-loaded packs.</summary>
    public static void ResetToEmbedded()
    {
        lock (RegistryLock)
        {
            _externalManifest = null;
            _externalLocales = null;
            _loadedRoot = null;
            _generation++;
            _threadActiveCode = null;
            _activeCode = EmbeddedManifest.Value.DefaultLocale;
        }
    }

    /// <summary>Loads a single locale pack from a directory on disk without registering it.</summary>
    public static LocalePack LoadFromDirectory(string localeDirectory) =>
        LocalePack.FromDirectory(localeDirectory);

    private static string ResolveLocaleDirectory(string root, ManifestLocale locale)
    {
        var relative = locale.Path.Replace('\\', '/').Trim('/');
        if (relative.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(root, relative);

        return Path.Combine(root, "data", locale.Code);
    }

    private static ManifestDocument LoadEmbeddedManifest()
    {
        using var stream = typeof(WordbankCatalog).Assembly
            .GetManifestResourceStream("SoundScript.Wordbank.Data.manifest.json")
            ?? throw new InvalidOperationException("Embedded manifest.json not found.");

        return LocalePack.Deserialize<ManifestDocument>(stream);
    }

    private static IReadOnlyDictionary<string, LocalePack> LoadEmbeddedLocales()
    {
        var assembly = typeof(WordbankCatalog).Assembly;
        var locales = new Dictionary<string, LocalePack>(StringComparer.Ordinal);

        foreach (var locale in EmbeddedManifest.Value.Locales)
            locales[locale.Code] = LocalePack.FromEmbeddedResources(assembly, locale.Code);

        return locales;
    }
}
