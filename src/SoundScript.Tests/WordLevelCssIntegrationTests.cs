using System.Security.Cryptography;
using SoundScript.Timbre;
using SoundScript.Vocal;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

/// <summary>
/// End-to-end: word-level SoundCSS rules supplied via <c>--css</c> actually reach
/// the vocal engines and change (deterministically) the rendered audio.
/// </summary>
[Collection("WordbankCatalog")]
public class WordLevelCssIntegrationTests : IDisposable
{
    private const string Text = "jingle bells";

    public WordLevelCssIntegrationTests()
    {
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
        CorpusCatalog.Reset();
        CorpusCatalog.TryLoadEmbedded();
    }

    public void Dispose()
    {
        CorpusCatalog.Reset();
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
    }

    private static IReadOnlyDictionary<string, SoundCssPronunciation> Rules() =>
        SoundCSSParser.ParsePronunciations(
            """
            "jingle" { style: sing; pitch: +6; vibrato: strong; persona: robot; }
            "bells"  { gender: female; timbre: dark; persona: robot; }
            """);

    [Fact]
    public void WordbankEngine_WithCss_ChangesRenderedAudio()
    {
        using var dir = new TempDir();

        var plain = dir.File("plain.wav");
        var styled = dir.File("styled.wav");

        new WordbankVocalEngine().Synthesize(Text, plain, new VocalEngineOptions { Locale = "en" });
        new WordbankVocalEngine().Synthesize(
            Text, styled, new VocalEngineOptions { Locale = "en", Pronunciations = Rules() });

        Assert.NotEqual(Hash(plain), Hash(styled));
    }

    [Fact]
    public void WordbankEngine_WithCss_IsDeterministic()
    {
        using var dir = new TempDir();
        var a = dir.File("a.wav");
        var b = dir.File("b.wav");

        var options = new VocalEngineOptions { Locale = "en", Pronunciations = Rules() };
        new WordbankVocalEngine().Synthesize(Text, a, options);
        new WordbankVocalEngine().Synthesize(Text, b, options);

        Assert.Equal(Hash(a), Hash(b));
    }

    [Fact]
    public void CompositeEngine_WithCss_ChangesRenderedAudio()
    {
        using var dir = new TempDir();
        var plain = dir.File("plain.wav");
        var styled = dir.File("styled.wav");

        new CompositeVocalEngine().Synthesize(Text, plain, new VocalEngineOptions { Locale = "en" });
        new CompositeVocalEngine().Synthesize(
            Text, styled, new VocalEngineOptions { Locale = "en", Pronunciations = Rules() });

        Assert.NotEqual(Hash(plain), Hash(styled));
    }

    [Fact]
    public void NoMatchingWordRule_LeavesAudioUnchanged()
    {
        using var dir = new TempDir();
        var plain = dir.File("plain.wav");
        var styled = dir.File("styled.wav");

        var unrelated = SoundCSSParser.ParsePronunciations("\"xylophone\" { style: shout; }");

        new WordbankVocalEngine().Synthesize(Text, plain, new VocalEngineOptions { Locale = "en" });
        new WordbankVocalEngine().Synthesize(
            Text, styled, new VocalEngineOptions { Locale = "en", Pronunciations = unrelated });

        Assert.Equal(Hash(plain), Hash(styled));
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class TempDir : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ss-css-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(_root);

        public string File(string name) => Path.Combine(_root, name);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
