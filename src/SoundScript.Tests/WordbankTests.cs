using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

public class WordbankTests
{
    [Fact]
    public void DefaultLocale_LoadsEmbeddedEnglishPack()
    {
        var locale = WordbankCatalog.Default;

        Assert.Equal("en", locale.Code);
        Assert.Contains("the", locale.FunctionWordSet);
        Assert.Contains("bl", locale.LegalOnsetSet);
        Assert.True(locale.ComposeGestureMap.ContainsKey("aa"));
        Assert.True(locale.WaveFrequencyMap.ContainsKey("aa"));
    }

    [Fact]
    public void GraphemePhonemeEngine_MatchesComposeSplitter()
    {
        var rules = WordbankCatalog.Default.GraphemeRules;
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
}
