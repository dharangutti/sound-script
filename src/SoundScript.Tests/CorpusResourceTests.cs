using SoundScript.Vocal;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public sealed class CorpusResourceTests : IDisposable
{
    public CorpusResourceTests() { WordbankCatalog.ResetToEmbedded(); CorpusCatalog.Reset(); }
    public void Dispose() { CorpusCatalog.Reset(); WordbankCatalog.ResetToEmbedded(); }

    [Fact]
    public void AssemblyPlaybackMatchesDiskWithoutCreatingFiles()
    {
        Assert.True(CorpusCatalog.TryLoadEmbedded());
        var options = new VocalEngineOptions { Locale = "en" };
        var disk = new WordbankVocalEngine().SynthesizeToWavBytes("hello welcome", options);
        var locales = new[] { "en", "hi", "kn", "mr", "ta", "te" };
        var diskAudio = new Dictionary<string, byte[]>();
        foreach (var locale in locales)
            foreach (var key in CorpusCatalog.GetLemmaKeys(locale))
            {
                Assert.True(CorpusCatalog.TryGetLemma(locale, key, out var entry));
                if (CorpusCatalog.TryGetAudioBytes(entry, out var wav)) diskAudio[$"{locale}/{key}"] = wav;
            }
        Assert.NotEmpty(diskAudio);
        CorpusCatalog.Reset();
        Assert.True(CorpusCatalog.TryLoadAssemblyResources());
        foreach (var (key, expected) in diskAudio)
        {
            var parts = key.Split('/');
            Assert.True(CorpusCatalog.TryGetLemma(parts[0], parts[1], out var entry));
            Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var actual));
            Assert.Equal(expected, actual);
        }
        Assert.Equal(disk, new WordbankVocalEngine().SynthesizeToWavBytes("hello welcome", options));
        Assert.Null(CorpusCatalog.LoadedRoot);
        Assert.True(CorpusCatalog.Reload());
        Assert.Null(CorpusCatalog.LoadedRoot);
    }

    [Fact]
    public void ExplicitPathRequestsMaterializeOwnedWritableData()
    {
        Assert.True(CorpusCatalog.TryLoadAssemblyResources());
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var resource));
        var audioPath = CorpusCatalog.ResolveAudioPath(entry);
        Assert.NotNull(audioPath);
        Assert.StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundScript", "corpus"), audioPath);
        Assert.Equal(resource, File.ReadAllBytes(audioPath));
        Assert.True(File.Exists(CorpusCatalog.ResolveLemmaFilePath("en")));
        Assert.StartsWith(CorpusCatalog.LoadedRoot!, CorpusCatalog.ResolveNormalizedAudioPath("en", "hello"));
        Assert.True(CorpusCatalog.Reload());
    }

    [Fact]
    public void RegisteredAudioOverridesAssemblyResourceAndUnknownSnapshotFails()
    {
        Assert.False(CorpusCatalog.TryLoadAssemblyResources("missing"));
        Assert.True(CorpusCatalog.TryLoadAssemblyResources());
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        CorpusCatalog.RegisterAudio(entry.Audio!, [1, 2, 3]);
        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var bytes));
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Null(CorpusCatalog.LoadedRoot);
    }

    [Fact]
    public void RegisteredAndReturnedAudioAreOwnedCopies()
    {
        Assert.True(CorpusCatalog.TryLoadAssemblyResources());
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        byte[] caller = [1, 2, 3];
        CorpusCatalog.RegisterAudio(entry.Audio!, caller);
        caller[0] = 99;
        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var first));
        Assert.Equal(new byte[] { 1, 2, 3 }, first);
        first[1] = 99;
        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var second));
        Assert.Equal(new byte[] { 1, 2, 3 }, second);
    }
}
