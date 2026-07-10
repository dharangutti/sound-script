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
