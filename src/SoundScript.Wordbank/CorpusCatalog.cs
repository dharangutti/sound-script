using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

/// <summary>
/// Loads curated per-word pronunciation metadata and audio paths from
/// <c>corpus/vYYYY.MM/</c> inside a wordbank checkout or embedded data.
/// </summary>
public static class CorpusCatalog
{
    private const string DefaultCorpusId = "2026.07";

    private static readonly object LoadLock = new();
    private static string? _loadedRoot;
    private static string _corpusId = DefaultCorpusId;
    private static CorpusManifestDocument? _manifest;
    private static Dictionary<string, Dictionary<string, CorpusLemmaEntry>>? _lemmaIndex;

    /// <summary>True when lemma metadata has been loaded.</summary>
    public static bool IsLoaded => _lemmaIndex is not null;

    /// <summary>Root directory containing <c>corpus/</c> (wordbank checkout or embedded parent).</summary>
    public static string? LoadedRoot => _loadedRoot;

    /// <summary>Active corpus snapshot id (e.g. <c>2026.07</c>).</summary>
    public static string CorpusId => _corpusId;

    /// <summary>
    /// Loads corpus metadata from a wordbank repository root. Safe to call repeatedly;
    /// replaces any prior load.
    /// </summary>
    public static bool TryLoadFromWordbankRoot(string wordbankRoot, out string? error, string corpusId = DefaultCorpusId)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(wordbankRoot))
        {
            error = "Wordbank root path is empty.";
            return false;
        }

        var root = Path.GetFullPath(wordbankRoot);
        var corpusRoot = Path.Combine(root, "corpus", $"v{corpusId}");
        return TryLoadFromCorpusRoot(corpusRoot, corpusId, error: out error);
    }

    /// <summary>Loads corpus metadata from an embedded or output <c>Data/corpus/vYYYY.MM</c> tree.</summary>
    public static bool TryLoadEmbedded(string corpusId = DefaultCorpusId)
    {
        var assembly = typeof(CorpusCatalog).Assembly;
        var baseDir = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrEmpty(baseDir))
            return false;

        var corpusRoot = Path.Combine(baseDir, "Data", "corpus", $"v{corpusId}");
        return TryLoadFromCorpusRoot(corpusRoot, corpusId, error: out _);
    }

    /// <summary>Re-reads corpus metadata from the currently loaded root (picks up on-disk edits).</summary>
    public static bool Reload()
    {
        if (_loadedRoot is null)
            return false;

        return TryLoadFromCorpusRoot(_loadedRoot, _corpusId, out _);
    }

    /// <summary>
    /// Resolves the on-disk lemma file (e.g. <c>en/lemmas.json</c>) for a locale
    /// under the loaded corpus root, or null when the locale/root is unknown.
    /// </summary>
    public static string? ResolveLemmaFilePath(string localeCode)
    {
        EnsureLoaded();
        if (_loadedRoot is null || _manifest is null)
            return null;

        foreach (var locale in _manifest.Locales)
        {
            if (string.Equals(locale.Code, localeCode, StringComparison.OrdinalIgnoreCase))
                return Path.Combine(_loadedRoot, locale.Path, locale.LemmaFile);
        }

        return null;
    }

    /// <summary>
    /// Inserts or replaces a lemma entry in the in-memory index so lookups resolve
    /// immediately after generation, without a full reload.
    /// </summary>
    public static void UpsertLemma(string localeCode, CorpusLemmaEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Lemma))
            return;

        EnsureLoaded();
        lock (LoadLock)
        {
            _lemmaIndex ??= new Dictionary<string, Dictionary<string, CorpusLemmaEntry>>(StringComparer.Ordinal);
            if (!_lemmaIndex.TryGetValue(localeCode, out var localeMap))
            {
                localeMap = new Dictionary<string, CorpusLemmaEntry>(StringComparer.OrdinalIgnoreCase);
                _lemmaIndex[localeCode] = localeMap;
            }

            localeMap[entry.Lemma] = entry;
        }
    }

    /// <summary>Clears loaded corpus metadata.</summary>
    public static void Reset()
    {
        lock (LoadLock)
        {
            _loadedRoot = null;
            _manifest = null;
            _lemmaIndex = null;
            _corpusId = DefaultCorpusId;
        }
    }

    /// <summary>Returns all known lemma keys for a locale (sorted), or empty when unknown.</summary>
    public static IReadOnlyList<string> GetLemmaKeys(string localeCode)
    {
        EnsureLoaded();
        if (_lemmaIndex is null || !_lemmaIndex.TryGetValue(localeCode, out var localeMap))
            return [];

        return localeMap.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Looks up a lemma entry for the given locale (case-insensitive).</summary>
    public static bool TryGetLemma(string localeCode, string lemma, out CorpusLemmaEntry entry)
    {
        entry = null!;
        EnsureLoaded();

        if (_lemmaIndex is null)
            return false;

        if (!_lemmaIndex.TryGetValue(localeCode, out var localeMap))
            return false;

        return localeMap.TryGetValue(lemma, out entry!);
    }

    /// <summary>Resolves the on-disk WAV path for a lemma entry, or null when unavailable.</summary>
    public static string? ResolveAudioPath(CorpusLemmaEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Audio) || _loadedRoot is null)
            return null;

        var path = Path.Combine(_loadedRoot, entry.Audio.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Resolves the canonical output path for a normalized lemma WAV under
    /// <c>corpus/vYYYY.MM/audio/{locale}/normalized/{lemma}.wav</c>. Returns null
    /// when no corpus root is loaded. The directory is not created here.
    /// </summary>
    public static string? ResolveNormalizedAudioPath(string localeCode, string lemma)
    {
        EnsureLoaded();
        if (_loadedRoot is null || string.IsNullOrWhiteSpace(localeCode) || string.IsNullOrWhiteSpace(lemma))
            return null;

        return Path.Combine(_loadedRoot, "audio", localeCode, "normalized", lemma + ".wav");
    }

    private static void EnsureLoaded()
    {
        if (_lemmaIndex is not null)
            return;

        lock (LoadLock)
        {
            if (_lemmaIndex is not null)
                return;

            if (WordbankCatalog.LoadedRoot is not null)
            {
                TryLoadFromWordbankRoot(WordbankCatalog.LoadedRoot, out _, DefaultCorpusId);
                if (_lemmaIndex is not null)
                    return;
            }

            TryLoadEmbedded(DefaultCorpusId);
        }
    }

    private static bool TryLoadFromCorpusRoot(string corpusRoot, string corpusId, out string? error)
    {
        error = null;
        var manifestPath = Path.Combine(corpusRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            error = $"Corpus manifest not found: {manifestPath}";
            return false;
        }

        try
        {
            var manifest = LocalePack.DeserializeFile<CorpusManifestDocument>(manifestPath);
            var index = new Dictionary<string, Dictionary<string, CorpusLemmaEntry>>(StringComparer.Ordinal);

            foreach (var locale in manifest.Locales)
            {
                var lemmaPath = Path.Combine(corpusRoot, locale.Path, locale.LemmaFile);
                if (!File.Exists(lemmaPath))
                    continue;

                var lemmas = LocalePack.DeserializeFile<CorpusLemmasDocument>(lemmaPath);
                var localeMap = new Dictionary<string, CorpusLemmaEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in lemmas.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Lemma))
                        localeMap[entry.Lemma] = entry;
                }

                index[locale.Code] = localeMap;
            }

            lock (LoadLock)
            {
                _loadedRoot = corpusRoot;
                _corpusId = corpusId;
                _manifest = manifest;
                _lemmaIndex = index;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
