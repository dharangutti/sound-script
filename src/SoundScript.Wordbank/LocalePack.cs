using System.Reflection;
using System.Text.Json;
using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

/// <summary>
/// One loaded locale pack from <c>soundscript-wordbank</c> JSON files.
/// </summary>
public sealed class LocalePack
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public FunctionWordsDocument FunctionWords { get; init; } = new();
    public StressPrefixesDocument StressPrefixes { get; init; } = new();
    public WordProsodyDocument WordProsody { get; init; } = new();
    public GraphemeRulesDocument GraphemeRules { get; init; } = new();
    public LegalOnsetsDocument LegalOnsets { get; init; } = new();
    public SyllabificationDocument Syllabification { get; init; } = new();
    public PhonemeComposeDocument PhonemeCompose { get; init; } = new();
    public PhonemeWaveDocument PhonemeWave { get; init; } = new();
    public PhonemeTimbreDocument PhonemeTimbre { get; init; } = new();
    public WordEntriesDocument WordEntries { get; init; } = new();

    public HashSet<string> FunctionWordSet { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LegalOnsetSet { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, WordEntry> WordEntryMap { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PhonemeGesture> ComposeGestureMap { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, PhonemeFrequency> WaveFrequencyMap { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, TimbreProfileRow> TimbreProfileMap { get; private set; } = new(StringComparer.Ordinal);

    internal static LocalePack FromDirectory(string localeDirectory)
    {
        var manifest = DeserializeFile<LocaleManifestDocument>(Path.Combine(localeDirectory, "locale.json"));
        var pack = new LocalePack
        {
            Code = manifest.Code,
            Name = manifest.Name,
            Version = manifest.Version,
            FunctionWords = DeserializeFile<FunctionWordsDocument>(Path.Combine(localeDirectory, manifest.Files.FunctionWords)),
            StressPrefixes = DeserializeFile<StressPrefixesDocument>(Path.Combine(localeDirectory, manifest.Files.StressPrefixes)),
            WordProsody = DeserializeFile<WordProsodyDocument>(Path.Combine(localeDirectory, manifest.Files.WordProsody)),
            GraphemeRules = DeserializeFile<GraphemeRulesDocument>(Path.Combine(localeDirectory, manifest.Files.GraphemeRules)),
            LegalOnsets = DeserializeFile<LegalOnsetsDocument>(Path.Combine(localeDirectory, manifest.Files.LegalOnsets)),
            Syllabification = DeserializeFile<SyllabificationDocument>(Path.Combine(localeDirectory, manifest.Files.Syllabification)),
            PhonemeCompose = DeserializeFile<PhonemeComposeDocument>(Path.Combine(localeDirectory, manifest.Files.PhonemeCompose)),
            PhonemeWave = DeserializeFile<PhonemeWaveDocument>(Path.Combine(localeDirectory, manifest.Files.PhonemeWave)),
            PhonemeTimbre = DeserializeFile<PhonemeTimbreDocument>(Path.Combine(localeDirectory, manifest.Files.PhonemeTimbre)),
            WordEntries = DeserializeFile<WordEntriesDocument>(Path.Combine(localeDirectory, manifest.Files.WordEntries)),
        };

        pack.BuildIndexes();
        return pack;
    }

    internal static LocalePack FromEmbeddedResources(Assembly assembly, string localeCode)
    {
        var prefix = $"SoundScript.Wordbank.Data.{localeCode}.";
        var manifest = DeserializeEmbedded<LocaleManifestDocument>(assembly, prefix + "locale.json");
        var pack = new LocalePack
        {
            Code = manifest.Code,
            Name = manifest.Name,
            Version = manifest.Version,
            FunctionWords = DeserializeEmbedded<FunctionWordsDocument>(assembly, prefix + manifest.Files.FunctionWords.Replace('/', '.').Replace("\\", ".")),
            StressPrefixes = DeserializeEmbedded<StressPrefixesDocument>(assembly, prefix + manifest.Files.StressPrefixes.Replace('/', '.').Replace("\\", ".")),
            WordProsody = DeserializeEmbedded<WordProsodyDocument>(assembly, prefix + manifest.Files.WordProsody.Replace('/', '.').Replace("\\", ".")),
            GraphemeRules = DeserializeEmbedded<GraphemeRulesDocument>(assembly, prefix + manifest.Files.GraphemeRules.Replace('/', '.').Replace("\\", ".")),
            LegalOnsets = DeserializeEmbedded<LegalOnsetsDocument>(assembly, prefix + manifest.Files.LegalOnsets.Replace('/', '.').Replace("\\", ".")),
            Syllabification = DeserializeEmbedded<SyllabificationDocument>(assembly, prefix + manifest.Files.Syllabification.Replace('/', '.').Replace("\\", ".")),
            PhonemeCompose = DeserializeEmbedded<PhonemeComposeDocument>(assembly, prefix + manifest.Files.PhonemeCompose.Replace('/', '.').Replace("\\", ".")),
            PhonemeWave = DeserializeEmbedded<PhonemeWaveDocument>(assembly, prefix + manifest.Files.PhonemeWave.Replace('/', '.').Replace("\\", ".")),
            PhonemeTimbre = DeserializeEmbedded<PhonemeTimbreDocument>(assembly, prefix + manifest.Files.PhonemeTimbre.Replace('/', '.').Replace("\\", ".")),
            WordEntries = DeserializeEmbedded<WordEntriesDocument>(assembly, prefix + manifest.Files.WordEntries.Replace('/', '.').Replace("\\", ".")),
        };

        pack.BuildIndexes();
        return pack;
    }

    private void BuildIndexes()
    {
        FunctionWordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var words in FunctionWords.Categories.Values)
        {
            foreach (var word in words)
                FunctionWordSet.Add(word);
        }

        LegalOnsetSet = new HashSet<string>(LegalOnsets.Onsets, StringComparer.OrdinalIgnoreCase);

        WordEntryMap = new Dictionary<string, WordEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in WordEntries.Entries)
            WordEntryMap[entry.Word] = entry;

        ComposeGestureMap = new Dictionary<string, PhonemeGesture>(StringComparer.Ordinal);
        foreach (var gesture in PhonemeCompose.Phonemes)
        {
            if (!string.IsNullOrEmpty(gesture.Phoneme))
                ComposeGestureMap[gesture.Phoneme] = gesture;
        }

        WaveFrequencyMap = new Dictionary<string, PhonemeFrequency>(StringComparer.Ordinal);
        foreach (var frequency in PhonemeWave.Phonemes)
        {
            if (!string.IsNullOrEmpty(frequency.Phoneme))
                WaveFrequencyMap[frequency.Phoneme] = frequency;
        }

        TimbreProfileMap = new Dictionary<string, TimbreProfileRow>(StringComparer.Ordinal);
        foreach (var profile in PhonemeTimbre.Phonemes)
        {
            if (!string.IsNullOrEmpty(profile.Phoneme))
                TimbreProfileMap[profile.Phoneme] = profile;
        }
    }

    private static T DeserializeFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return Deserialize<T>(json);
    }

    internal static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize JSON document.");

    internal static T Deserialize<T>(Stream stream) =>
        JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize JSON stream.");

    private static T DeserializeEmbedded<T>(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return Deserialize<T>(stream);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
