using System.Text.Json;
using SoundScript.Vocal.Wordbank;
using SoundScript.Wave.Io;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public class WordbankNormalizerTests : IDisposable
{
    private const string Lemma = "hello";
    private const string Locale = "en";

    public WordbankNormalizerTests()
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
    public void Normalize_KnownLemma_WritesCanonicalWavUnderNormalizedFolder()
    {
        var result = new WordbankNormalizer().Normalize(Lemma, Locale);

        Assert.True(result.Success, result.Reason);
        Assert.False(result.Missing);
        Assert.NotNull(result.Path);
        Assert.True(File.Exists(result.Path));

        var normalizedDir = Path.Combine("audio", Locale, "normalized");
        Assert.Contains(normalizedDir, result.Path!.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
        Assert.EndsWith($"{Lemma}.wav", result.Path);
    }

    [Fact]
    public void Normalize_MissingLemma_ReturnsClearMissingResult()
    {
        var result = new WordbankNormalizer().Normalize("zzz-not-a-real-lemma", Locale);

        Assert.False(result.Success);
        Assert.True(result.Missing);
        Assert.Null(result.Path);
        Assert.Null(result.Metadata);
        Assert.NotNull(result.Reason);
        Assert.Contains("zzz-not-a-real-lemma", result.Reason!);
    }

    [Fact]
    public void Normalize_IsBitStable_AcrossIdenticalRuns()
    {
        var normalizer = new WordbankNormalizer();

        var first = normalizer.Normalize(Lemma, Locale);
        Assert.True(first.Success, first.Reason);
        var firstWav = File.ReadAllBytes(first.Path!);
        var firstMeta = File.ReadAllBytes(Path.ChangeExtension(first.Path!, ".json"));

        var second = normalizer.Normalize(Lemma, Locale);
        Assert.True(second.Success, second.Reason);
        var secondWav = File.ReadAllBytes(second.Path!);
        var secondMeta = File.ReadAllBytes(Path.ChangeExtension(second.Path!, ".json"));

        Assert.Equal(firstWav, secondWav);
        Assert.Equal(firstMeta, secondMeta);
    }

    [Fact]
    public void Normalize_WritesMetadataSidecar_MatchingResultMetadata()
    {
        var result = new WordbankNormalizer().Normalize(Lemma, Locale);
        Assert.True(result.Success, result.Reason);

        var metadataPath = Path.ChangeExtension(result.Path!, ".json");
        Assert.True(File.Exists(metadataPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath));
        var root = doc.RootElement;

        // All required metadata keys are present.
        Assert.True(root.TryGetProperty("normalized", out var normalized));
        Assert.True(root.TryGetProperty("basePitchHz", out _));
        Assert.True(root.TryGetProperty("durationMs", out _));
        Assert.True(root.TryGetProperty("energyRMS", out _));
        Assert.True(root.TryGetProperty("normalizerVersion", out var version));

        Assert.True(normalized.GetBoolean());
        Assert.Equal(WordbankNormalizer.NormalizerVersion, version.GetInt32());

        // Sidecar values agree with the returned metadata object.
        Assert.Equal(result.Metadata!.BasePitchHz, root.GetProperty("basePitchHz").GetDouble(), 6);
        Assert.Equal(result.Metadata.DurationMs, root.GetProperty("durationMs").GetDouble(), 6);
        Assert.Equal(result.Metadata.EnergyRms, root.GetProperty("energyRMS").GetDouble(), 6);
    }

    [Fact]
    public void Normalize_Metadata_HasPlausibleValues()
    {
        var result = new WordbankNormalizer().Normalize(Lemma, Locale);
        Assert.True(result.Success, result.Reason);

        var metadata = result.Metadata!;
        Assert.True(metadata.Normalized);
        Assert.Equal(WordbankNormalizer.NormalizerVersion, metadata.NormalizerVersion);

        // Duration must be positive and reasonable for a single spoken word.
        Assert.InRange(metadata.DurationMs, 1.0, 10_000.0);

        // A voiced word should carry energy; RMS is a linear 0..1 amplitude.
        Assert.InRange(metadata.EnergyRms, 1e-4, 1.0);

        // Base pitch is either "unvoiced" (0) or within the human voice band.
        Assert.True(
            metadata.BasePitchHz == 0.0 || (metadata.BasePitchHz is >= 50.0 and <= 500.0),
            $"Implausible base pitch: {metadata.BasePitchHz} Hz");
    }

    [Fact]
    public void Normalize_RespectsPeakCeiling()
    {
        var options = new WordbankNormalizerOptions { TargetPeakDbFs = -1.0 };
        var result = new WordbankNormalizer(options).Normalize(Lemma, Locale);
        Assert.True(result.Success, result.Reason);

        var samples = WavReader.ReadMono(result.Path!);
        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        var ceiling = Math.Pow(10.0, -1.0 / 20.0);
        // Allow one 16-bit quantization step of headroom above the target.
        Assert.True(peak <= ceiling + 1.0 / short.MaxValue, $"Peak {peak} exceeded ceiling {ceiling}.");
    }

    [Fact]
    public void DetectBasePitch_OnSyntheticTone_IsAccurateAndDeterministic()
    {
        const int sampleRate = WavWriter.SampleRate;
        const double toneHz = 220.0;
        var tone = new float[sampleRate / 2];
        for (var i = 0; i < tone.Length; i++)
            tone[i] = (float)(0.5 * Math.Sin(2 * Math.PI * toneHz * i / sampleRate));

        var pitchA = AudioNormalizeOps.DetectBasePitchHz(tone, sampleRate);
        var pitchB = AudioNormalizeOps.DetectBasePitchHz(tone, sampleRate);

        Assert.Equal(pitchA, pitchB);
        Assert.InRange(pitchA, toneHz - 3.0, toneHz + 3.0);
    }

    [Fact]
    public void TrimSilence_RemovesLeadingAndTrailingSilence()
    {
        const int sampleRate = WavWriter.SampleRate;
        var signal = new float[sampleRate];
        for (var i = sampleRate / 4; i < sampleRate / 2; i++)
            signal[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 200 * i / sampleRate));

        var trimmed = AudioNormalizeOps.TrimSilence(signal, threshold: 0.01, paddingMs: 0, sampleRate);

        Assert.True(trimmed.Length < signal.Length);
        Assert.True(AudioNormalizeOps.Peak(trimmed) > 0.4);
    }
}
