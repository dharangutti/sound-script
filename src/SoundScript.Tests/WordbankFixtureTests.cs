using System.Text.Json;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public class WordbankFixtureTests : IDisposable
{
    public WordbankFixtureTests()
    {
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
    }

    public void Dispose()
    {
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
    }

    [Fact]
    public void PackageVersion_IsAtLeast050()
    {
        var parts = WordbankCatalog.PackageVersion.Split('.');
        Assert.True(parts.Length >= 2);
        Assert.True(int.Parse(parts[0]) >= 0);
        Assert.True(int.Parse(parts[1]) >= 5);
    }

    [Fact]
    public void CiFixtures_SpanishWords_InCommonDictionary()
    {
        var fixtures = LoadCiFixtures();
        var map = WordbankCatalog.GetLocale("es").WordEntryMap;
        foreach (var word in fixtures.Es)
            Assert.True(map.ContainsKey(word), $"Missing Spanish fixture word: {word}");
    }

    [Fact]
    public void CiFixtures_FrenchWords_InCommonDictionary()
    {
        var fixtures = LoadCiFixtures();
        var map = WordbankCatalog.GetLocale("fr").WordEntryMap;
        foreach (var word in fixtures.French)
            Assert.True(map.ContainsKey(word), $"Missing French fixture word: {word}");
    }

    [Fact]
    public void CiFixtures_EnglishWords_InPilotOrCommon()
    {
        var root = ResolveWordbankRoot();
        var fixtures = LoadCiFixtures(root);
        var pilot = LoadEnglishPilot(root);
        var common = WordbankCatalog.GetLocale("en").WordEntryMap.Keys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var word in fixtures.En)
        {
            Assert.True(
                common.Contains(word) || pilot.Contains(word.ToLowerInvariant()),
                $"English fixture word '{word}' not in common.json or pilot-1k.txt");
        }
    }

    [Fact]
    public void CorpusPilot_EnglishHas1000Lemmas()
    {
        var root = ResolveWordbankRoot();
        var pilot = LoadEnglishPilot(root);
        Assert.Equal(1000, pilot.Count);
    }

    private static CiFixturesDocument LoadCiFixtures(string? root = null)
    {
        root ??= ResolveWordbankRoot();
        var path = Path.Combine(root, "fixtures", "ci-50.json");
        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<CiFixturesDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse ci-50.json");
        Assert.Equal(50, doc.Fixtures.En.Count);
        Assert.Equal(50, doc.Fixtures.Es.Count);
        Assert.Equal(50, doc.Fixtures.Fr.Count);
        return doc;
    }

    private static HashSet<string> LoadEnglishPilot(string root)
    {
        var path = Path.Combine(root, "corpus", "v2026.07", "en", "pilot-1k.txt");
        return File.ReadAllLines(path)
            .Select(line => line.Trim().ToLowerInvariant())
            .Where(line => line.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void CorpusCatalog_CoversMostCiFixtureWords()
    {
        var lemmasPath = ResolveWordbankRoot();
        var document = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            File.ReadAllText(Path.Combine(lemmasPath, "corpus/v2026.07/en/lemmas.json")));
        var lemmas = document.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("lemma").GetString()!.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fixturesPath = Path.Combine(lemmasPath, "fixtures/ci-50.json");
        var fixtures = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            File.ReadAllText(fixturesPath));
        var enWords = fixtures.GetProperty("fixtures").GetProperty("en").EnumerateArray()
            .Select(e => e.GetString()!.ToLowerInvariant())
            .ToList();

        var covered = enWords.Count(word => lemmas.Contains(word));
        Assert.True(covered >= 26, $"Expected at least 26/50 CI English words in corpus, found {covered}.");
    }

    private static string ResolveWordbankRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var name in new[] { "wordbank", "../soundscript-wordbank" })
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, name));
                if (File.Exists(Path.Combine(candidate, "fixtures", "ci-50.json")))
                    return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate soundscript-wordbank root for fixture tests.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class CiFixturesDocument
    {
        public FixtureLocales Fixtures { get; init; } = new();
        public IReadOnlyList<string> En => Fixtures.En;
        public IReadOnlyList<string> Es => Fixtures.Es;
        public IReadOnlyList<string> French => Fixtures.Fr;
    }

    private sealed class FixtureLocales
    {
        public List<string> En { get; init; } = [];
        public List<string> Es { get; init; } = [];
        public List<string> Fr { get; init; } = [];
    }
}
