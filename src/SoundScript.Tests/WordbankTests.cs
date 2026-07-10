using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

public class WordbankTests
{
    public WordbankTests()
    {
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
}
