using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public class WordbankTests : IDisposable
{
    public WordbankTests()
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
    public void DefaultLocale_LoadsEmbeddedEnglishPack()
    {
        var locale = WordbankCatalog.Active;

        Assert.Equal("en", locale.Code);
        Assert.Contains("the", locale.FunctionWordSet);
        Assert.Contains("bl", locale.LegalOnsetSet);
        Assert.True(locale.ComposeGestureMap.ContainsKey("aa"));
        Assert.True(locale.WaveFrequencyMap.ContainsKey("aa"));
    }

    [Fact]
    public void AvailableLocales_IncludesEnglishSpanishFrench()
    {
        Assert.Equal(["en", "es", "fr"], WordbankCatalog.AvailableLocales);
    }

    [Fact]
    public void TrySetActive_SwitchesFunctionWords()
    {
        Assert.True(WordbankCatalog.TrySetActive("es", out _));
        Assert.Contains("el", WordbankCatalog.Active.FunctionWordSet);
        Assert.DoesNotContain("the", WordbankCatalog.Active.FunctionWordSet);

        WordbankCatalog.ResetActive();
        Assert.Contains("the", WordbankCatalog.Active.FunctionWordSet);
    }

    [Fact]
    public void TrySetActive_RejectsUnknownLocale()
    {
        Assert.False(WordbankCatalog.TrySetActive("de", out var error));
        Assert.Contains("de", error);
    }

    [Fact]
    public void GraphemePhonemeEngine_MatchesComposeSplitter()
    {
        var rules = WordbankCatalog.Active.GraphemeRules;
        var fromEngine = GraphemePhonemeEngine.Split("little", rules);
        var fromCompose = SoundScript.Compose.PhonemeSplitter.Split("little");

        Assert.Equal(fromCompose, fromEngine);
    }

    [Fact]
    public void WordEntry_OverridesStressForReturn()
    {
        var stress = SoundScript.Prosody.StressDetector.Detect("return", ["re", "turn"]);
        Assert.Equal(
            [SoundScript.Prosody.StressLevel.Unstressed, SoundScript.Prosody.StressLevel.Primary],
            stress);
    }

    [Fact]
    public void Syllabifier_WordEntry_PreservesInputCasing()
    {
        var syllables = SoundScript.Core.Phonetics.Syllabifier.Syllabify("Twinkle");
        Assert.Equal(["Twin", "kle"], syllables);
    }

    [Fact]
    public void CommonDictionary_IncludesWonderful()
    {
        Assert.True(WordbankCatalog.Active.WordEntryMap.ContainsKey("wonderful"));
    }

    [Fact]
    public void SpanishLocale_LoadsDemoWords()
    {
        var locale = WordbankCatalog.GetLocale("es");
        Assert.Equal("es", locale.Code);
        Assert.Contains("hola", locale.WordEntryMap.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("el", locale.FunctionWordSet);
    }

    [Fact]
    public void FrenchLocale_LoadsDemoWords()
    {
        var locale = WordbankCatalog.GetLocale("fr");
        Assert.Equal("fr", locale.Code);
        Assert.Contains("bonjour", locale.WordEntryMap.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("le", locale.FunctionWordSet);
    }

    [Fact]
    public void ActiveLocale_LoadsSyllabificationRules()
    {
        var rules = WordbankCatalog.Active.Syllabification;
        Assert.Equal("en", rules.Locale);
        Assert.True(rules.SilentTerminalE);
        Assert.Contains("a", rules.VowelLetters);
    }

    [Fact]
    public void ActiveLocale_LoadsPhonemeTimbreProfiles()
    {
        var timbre = WordbankCatalog.Active.PhonemeTimbre;
        Assert.Equal("en", timbre.Locale);
        Assert.Equal(550, timbre.Default.Formant1Hz);
        Assert.Contains(timbre.Phonemes, row => row.Phoneme == "aa");
        Assert.True(WordbankCatalog.Active.TimbreProfileMap.ContainsKey("aa"));
    }

    [Fact]
    public void TrySetActive_SwitchesSyllabificationRules()
    {
        Assert.True(WordbankCatalog.TrySetActive("es", out _));
        Assert.False(WordbankCatalog.Active.Syllabification.SilentTerminalE);
        Assert.Contains("á", WordbankCatalog.Active.Syllabification.AccentedVowels);

        Assert.True(WordbankCatalog.TrySetActive("fr", out _));
        Assert.Contains("eau", WordbankCatalog.Active.Syllabification.NucleusDigraphs);
        Assert.True(WordbankCatalog.Active.Syllabification.TreatYAsVowel);
    }

    [Fact]
    public void SpanishSyllabifier_UsesLocaleRulesWithoutSilentE()
    {
        Assert.True(WordbankCatalog.TrySetActive("es", out _));
        var syllables = SoundScript.Core.Phonetics.Syllabifier.Syllabify("gracias");
        Assert.Equal(["gra", "cias"], syllables);
    }

    [Fact]
    public void FrenchSyllabifier_UsesNucleusDigraphs()
    {
        Assert.True(WordbankCatalog.TrySetActive("fr", out _));
        var syllables = SoundScript.Core.Phonetics.Syllabifier.Syllabify("chanson");
        Assert.Equal(["chan", "son"], syllables);
    }

    [Fact]
    public void FrenchSyllabifier_WordEntryOverride_PreservesCasing()
    {
        Assert.True(WordbankCatalog.TrySetActive("fr", out _));
        var syllables = SoundScript.Core.Phonetics.Syllabifier.Syllabify("Bonjour");
        Assert.Equal(["Bon", "jour"], syllables);
    }

    [Fact]
    public void TryLoadFromRoot_LoadsLocalesFromDisk()
    {
        var root = ResolveWordbankRoot();
        Assert.True(WordbankCatalog.TryLoadFromRoot(root, out var error), error);
        Assert.True(WordbankCatalog.IsExternal);
        Assert.Equal(root, WordbankCatalog.LoadedRoot);
        Assert.Equal(["en", "es", "fr"], WordbankCatalog.AvailableLocales);
        Assert.Equal("en", WordbankCatalog.ActiveLocaleCode);
    }

    [Fact]
    public void TryLoadFromRoot_SwitchesActiveLocale()
    {
        var root = ResolveWordbankRoot();
        Assert.True(WordbankCatalog.TryLoadFromRoot(root, out _));
        Assert.True(WordbankCatalog.TrySetActive("es", out _));
        Assert.Contains("el", WordbankCatalog.Active.FunctionWordSet);
    }

    [Fact]
    public void ResetToEmbedded_RestoresEmbeddedCatalog()
    {
        var root = ResolveWordbankRoot();
        Assert.True(WordbankCatalog.TryLoadFromRoot(root, out _));
        Assert.True(WordbankCatalog.IsExternal);

        WordbankCatalog.ResetToEmbedded();
        Assert.False(WordbankCatalog.IsExternal);
        Assert.Null(WordbankCatalog.LoadedRoot);
        Assert.Equal("en", WordbankCatalog.ActiveLocaleCode);
    }

    [Fact]
    public void TryLoadFromRoot_RejectsMissingManifest()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"ss-wordbank-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            Assert.False(WordbankCatalog.TryLoadFromRoot(temp, out var error));
            Assert.Contains("manifest", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void SpanishCommonDictionary_IncludesGracias()
    {
        var locale = WordbankCatalog.GetLocale("es");
        Assert.True(locale.WordEntryMap.ContainsKey("gracias"));
        Assert.True(locale.WordEntryMap.Count >= 25);
    }

    [Fact]
    public void FrenchCommonDictionary_IncludesBonsoir()
    {
        var locale = WordbankCatalog.GetLocale("fr");
        Assert.True(locale.WordEntryMap.ContainsKey("bonsoir"));
        Assert.True(locale.WordEntryMap.Count >= 25);
    }

    private static string ResolveWordbankRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var name in new[] { "wordbank", "../soundscript-wordbank" })
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, name));
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                    return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate soundscript-wordbank root for tests.");
    }
}
