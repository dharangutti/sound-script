using System.Text.Json;
using SoundScript.Wave.Io;
using SoundScript.Wordbank;

namespace SoundScript.Vocal.Wordbank;

/// <summary>
/// Produces a deterministic canonical WAV plus metadata for a corpus lemma and
/// persists both under <c>corpus/vYYYY.MM/audio/{locale}/normalized/</c>.
///
/// <para>
/// The pipeline is intentionally pure and side-effect-free apart from the two
/// output files: given identical source samples, options, and
/// <see cref="NormalizerVersion"/>, the rendered 16-bit PCM WAV is byte-stable.
/// Auto-generation of audio for lemmas that have no corpus recording is handled
/// separately (see the follow-up wordbank generator work); this normalizer only
/// reports a clear "missing" result in that case.
/// </para>
/// </summary>
public sealed class WordbankNormalizer
{
    /// <summary>
    /// Version stamp for the normalization algorithm. Bump when the DSP pipeline
    /// changes in a way that alters output bytes so callers can invalidate caches.
    /// </summary>
    public const int NormalizerVersion = 1;

    private const int SampleRate = WavWriter.SampleRate;
    private const double PitchEpsilon = 1e-6;
    private const double SilenceFloor = 1e-8;

    private readonly WordbankNormalizerOptions _options;

    public WordbankNormalizer(WordbankNormalizerOptions? options = null) =>
        _options = options ?? WordbankNormalizerOptions.Default;

    /// <summary>
    /// Normalizes the corpus recording for <paramref name="lemma"/> in
    /// <paramref name="locale"/> and writes the canonical WAV + metadata sidecar.
    /// </summary>
    public WordbankNormalizeResult Normalize(string lemma, string locale)
    {
        if (string.IsNullOrWhiteSpace(lemma))
            throw new ArgumentException("Lemma must not be empty.", nameof(lemma));
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale must not be empty.", nameof(locale));

        if (!CorpusCatalog.TryGetLemma(locale, lemma, out var entry))
            return WordbankNormalizeResult.MissingResult($"No corpus lemma entry for '{lemma}' in locale '{locale}'.");

        var sourcePath = CorpusCatalog.ResolveAudioPath(entry);
        if (sourcePath is null)
        {
            return WordbankNormalizeResult.MissingResult(
                $"Corpus audio file missing for lemma '{lemma}' (locale '{locale}'). " +
                "Auto-generation is handled by the wordbank generator.");
        }

        var samples = WavReader.ReadMono(sourcePath);
        return NormalizeSamples(samples, lemma, locale, entry.PitchSemitones);
    }

    /// <summary>
    /// Normalizes an arbitrary source WAV (e.g. a freshly generated eSpeak clip
    /// in staging) into the canonical location for <paramref name="lemma"/>. Used
    /// by the auto-generator when a lemma has no corpus recording.
    /// </summary>
    public WordbankNormalizeResult NormalizeFromFile(
        string sourceWavPath, string lemma, string locale, double pitchSemitones = 0.0)
    {
        if (string.IsNullOrWhiteSpace(sourceWavPath))
            throw new ArgumentException("Source WAV path must not be empty.", nameof(sourceWavPath));
        if (string.IsNullOrWhiteSpace(lemma))
            throw new ArgumentException("Lemma must not be empty.", nameof(lemma));
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale must not be empty.", nameof(locale));
        if (!File.Exists(sourceWavPath))
            return WordbankNormalizeResult.MissingResult($"Source WAV not found: {sourceWavPath}");

        var samples = WavReader.ReadMono(sourceWavPath);
        return NormalizeSamples(samples, lemma, locale, pitchSemitones);
    }

    private WordbankNormalizeResult NormalizeSamples(
        float[] samples, string lemma, string locale, double pitchSemitones)
    {
        var outputPath = CorpusCatalog.ResolveNormalizedAudioPath(locale, lemma)
            ?? throw new InvalidOperationException(
                "Corpus root is not loaded; cannot resolve normalized output path.");

        var normalized = Process(samples, pitchSemitones);
        var metadata = BuildMetadata(normalized);

        Persist(outputPath, normalized, metadata);

        return WordbankNormalizeResult.SuccessResult(outputPath, metadata);
    }

    /// <summary>Runs the deterministic DSP pipeline on raw mono samples.</summary>
    internal float[] Process(float[] samples, double pitchSemitones)
    {
        var silenceThreshold = DbToLinear(_options.SilenceThresholdDbFs);
        var trimmed = AudioNormalizeOps.TrimSilence(samples, silenceThreshold, _options.EdgePaddingMs, SampleRate);

        var centered = _options.ApplyPitchCentering && Math.Abs(pitchSemitones) > PitchEpsilon
            ? AudioNormalizeOps.PitchShift(trimmed, pitchSemitones)
            : trimmed;

        var targetPeak = DbToLinear(_options.TargetPeakDbFs);
        var targetRms = DbToLinear(_options.TargetRmsDbFs);
        return AudioNormalizeOps.NormalizeGain(centered, targetPeak, targetRms, SilenceFloor);
    }

    private static WordbankNormalizedMetadata BuildMetadata(float[] samples)
    {
        var basePitch = AudioNormalizeOps.DetectBasePitchHz(samples, SampleRate);
        var energyRms = AudioNormalizeOps.Rms(samples);
        var durationMs = samples.Length / (double)SampleRate * 1000.0;

        // Round for byte-stable metadata persistence across identical runs.
        return new WordbankNormalizedMetadata
        {
            Normalized = true,
            BasePitchHz = Math.Round(basePitch, 2, MidpointRounding.AwayFromZero),
            DurationMs = Math.Round(durationMs, 3, MidpointRounding.AwayFromZero),
            EnergyRms = Math.Round(energyRms, 6, MidpointRounding.AwayFromZero),
            NormalizerVersion = NormalizerVersion,
        };
    }

    private static void Persist(string outputPath, float[] samples, WordbankNormalizedMetadata metadata)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        WavWriter.Write(outputPath, samples);

        // Normalize line endings to "\n" so the sidecar is byte-stable across platforms.
        var json = JsonSerializer.Serialize(metadata, MetadataJsonOptions).Replace("\r\n", "\n") + "\n";
        var metadataPath = Path.ChangeExtension(outputPath, ".json");
        File.WriteAllText(metadataPath, json);
    }

    private static double DbToLinear(double dbFs) => Math.Pow(10.0, dbFs / 20.0);

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
    };
}
