using SoundScript.Vocal;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

/// <summary>
/// Verifies the in-memory corpus path that backs the browser's "Enable In-Browser
/// Vocal Engine" toggle: lemmas + audio registered in memory render byte-identically
/// to the on-disk corpus, with no filesystem access for the audio.
/// </summary>
[Collection("WordbankCatalog")]
public class WordbankInMemoryCorpusTests : IDisposable
{
    public WordbankInMemoryCorpusTests()
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

    [Fact]
    public void SynthesizeToWavBytes_ProducesValidWav()
    {
        var bytes = new WordbankVocalEngine().SynthesizeToWavBytes("hello", new VocalEngineOptions { Locale = "en" });

        Assert.True(bytes.Length > 44);
        Assert.Equal("RIFF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
        Assert.Equal("WAVE"u8.ToArray(), bytes.AsSpan(8, 4).ToArray());
    }

    [Fact]
    public void InMemoryCorpus_RendersIdenticallyToDisk()
    {
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var wavBytes));

        var options = new VocalEngineOptions { Locale = "en" };
        var fromDisk = new WordbankVocalEngine().SynthesizeToWavBytes("hello", options);

        // Rebuild the lemma from memory only, then register the audio bytes so the
        // synthesizer takes the in-memory path (HasAudio == true).
        CorpusCatalog.Reset();
        CorpusCatalog.UpsertLemma("en", entry);
        CorpusCatalog.RegisterAudio(entry.Audio!, wavBytes);

        Assert.True(CorpusCatalog.HasAudio(entry));
        var fromMemory = new WordbankVocalEngine().SynthesizeToWavBytes("hello", options);

        Assert.Equal(fromDisk, fromMemory);
    }

    [Fact]
    public void RegisteredAudio_TakesPrecedenceAndIsReturned()
    {
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        var payload = new byte[] { 1, 2, 3, 4 };
        CorpusCatalog.RegisterAudio(entry.Audio!, payload);

        Assert.True(CorpusCatalog.TryGetAudioBytes(entry, out var got));
        Assert.Equal(payload, got);
    }
}
