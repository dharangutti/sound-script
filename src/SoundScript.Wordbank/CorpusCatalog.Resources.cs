using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

public static partial class CorpusCatalog
{
    private static bool _usingResources;
    private static readonly Lazy<IReadOnlyDictionary<string, string>> CorpusResources = new(() =>
        typeof(CorpusCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Corpus/", StringComparison.Ordinal))
            .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal));

    private static Stream? OpenCorpusResource(string corpusId, string relativePath) =>
        CorpusResources.Value.TryGetValue($"Corpus/v{corpusId}/{relativePath.Replace('\\', '/')}", out var name)
            ? typeof(CorpusCatalog).Assembly.GetManifestResourceStream(name) : null;

    // Internal entry point also lets regression tests exercise installed-package behavior
    // when the source checkout has its traditional Data/corpus output tree.
    internal static bool TryLoadAssemblyResources(string corpusId = DefaultCorpusId)
    {
        using var manifestStream = OpenCorpusResource(corpusId, "manifest.json");
        if (manifestStream is null) return false;
        var manifest = LocalePack.Deserialize<CorpusManifestDocument>(manifestStream);
        var index = new Dictionary<string, Dictionary<string, CorpusLemmaEntry>>(StringComparer.Ordinal);
        foreach (var locale in manifest.Locales)
        {
            using var stream = OpenCorpusResource(corpusId, $"{locale.Path}/{locale.LemmaFile}");
            if (stream is null) continue;
            var lemmas = LocalePack.Deserialize<CorpusLemmasDocument>(stream);
            var entries = new Dictionary<string, CorpusLemmaEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in lemmas.Entries)
                if (!string.IsNullOrWhiteSpace(entry.Lemma)) entries[entry.Lemma] = entry;
            index[locale.Code] = entries;
        }
        lock (LoadLock)
        {
            _loadedRoot = null;
            _corpusId = corpusId;
            _manifest = manifest;
            _lemmaIndex = index;
            _usingResources = true;
        }
        return true;
    }

    // Playback reads resources directly. Only APIs explicitly asking for editable
    // filesystem paths materialize a private corpus, preserving normalization/generation.
    private static void EnsureWritableCorpus()
    {
        lock (LoadLock)
        {
            if (!_usingResources || _loadedRoot is not null) return;
            if (OperatingSystem.IsBrowser()) return;
            var assembly = typeof(CorpusCatalog).Assembly;
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoundScript", "corpus", assembly.ManifestModule.ModuleVersionId.ToString("N"), $"v{_corpusId}");
            var prefix = $"Corpus/v{_corpusId}/";
            foreach (var (logicalName, resourceName) in CorpusResources.Value)
            {
                if (!logicalName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var path = Path.GetFullPath(Path.Combine(root, logicalName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Corpus resource escapes its owned directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path)) continue; // Preserve a user's normalized/generated data.
                var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var source = assembly.GetManifestResourceStream(resourceName)!)
                    using (var target = File.Create(staging)) source.CopyTo(target);
                    try { File.Move(staging, path); }
                    catch (IOException) when (File.Exists(path)) { } // Another host won the extraction race.
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
            }
            _loadedRoot = root;
        }
    }
}
