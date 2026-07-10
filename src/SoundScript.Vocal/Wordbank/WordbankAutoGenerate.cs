using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SoundScript.Wordbank;
using SoundScript.Wordbank.Models;

namespace SoundScript.Vocal.Wordbank;

/// <summary>
/// Fills gaps in the corpus: when a lemma has no normalized WAV, it deterministically
/// synthesizes one with eSpeak, normalizes it through <see cref="WordbankNormalizer"/>,
/// persists it under <c>corpus/vYYYY.MM/audio/{locale}/normalized/{lemma}.wav</c>, and
/// records generator provenance on the lemma entry.
///
/// <para>Idempotent: once a normalized WAV exists it is never regenerated, so the
/// audio bytes, sidecar, and lemma metadata (including <c>generatedAt</c>) stay
/// stable across repeated runs.</para>
/// </summary>
public sealed class WordbankAutoGenerate
{
    /// <summary>Generator id recorded on generated lemma entries.</summary>
    public const string GeneratorName = "espeak-ng";

    private readonly WordbankNormalizer _normalizer;
    private readonly IEspeakRawSynthesizer _espeak;
    private readonly WordbankAutoGenerateOptions _options;

    public WordbankAutoGenerate(
        WordbankAutoGenerateOptions? options = null,
        WordbankNormalizer? normalizer = null,
        IEspeakRawSynthesizer? espeak = null)
    {
        _options = options ?? WordbankAutoGenerateOptions.Default;
        _normalizer = normalizer ?? new WordbankNormalizer(_options.NormalizerOptions);
        _espeak = espeak ?? new EspeakRawSynthesizer();
    }

    /// <summary>
    /// Ensures a normalized WAV exists for <paramref name="lemma"/> in
    /// <paramref name="locale"/>, generating one when missing and
    /// <paramref name="autoGenerateMissing"/> is enabled.
    /// </summary>
    public WordbankAutoGenerateResult EnsureLemma(string lemma, string locale, bool autoGenerateMissing)
    {
        if (string.IsNullOrWhiteSpace(lemma))
            throw new ArgumentException("Lemma must not be empty.", nameof(lemma));
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale must not be empty.", nameof(locale));

        var normalizedPath = CorpusCatalog.ResolveNormalizedAudioPath(locale, lemma);
        if (normalizedPath is null)
        {
            return WordbankAutoGenerateResult.Failed(
                "Corpus root is not loaded; cannot resolve normalized output path.");
        }

        // Idempotency: an existing normalized WAV is authoritative — never regenerate.
        if (File.Exists(normalizedPath))
            return WordbankAutoGenerateResult.AlreadyPresent(normalizedPath);

        if (!autoGenerateMissing)
        {
            return WordbankAutoGenerateResult.MissingDisabled(
                $"Lemma '{lemma}' ({locale}) has no normalized audio. " +
                "Pass --auto-generate-missing to synthesize it.");
        }

        if (!_espeak.IsAvailable)
        {
            return WordbankAutoGenerateResult.GeneratorUnavailable(
                "eSpeak is not installed. Install espeak-ng to auto-generate missing lemmas.");
        }

        var voice = string.IsNullOrWhiteSpace(_options.Voice) ? locale : _options.Voice;
        var stagingPath = ResolveStagingPath(locale, lemma);

        try
        {
            _espeak.Synthesize(lemma, voice, stagingPath);

            var normalized = _normalizer.NormalizeFromFile(stagingPath, lemma, locale);
            if (!normalized.Success || normalized.Path is null || normalized.Metadata is null)
            {
                return WordbankAutoGenerateResult.Failed(
                    normalized.Reason ?? $"Normalization failed for generated lemma '{lemma}'.");
            }

            var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var entry = PersistLemmaMetadata(locale, lemma, normalized.Metadata.NormalizerVersion, generatedAt);

            // Refresh in-memory index so an immediate subsequent lookup resolves.
            CorpusCatalog.UpsertLemma(locale, entry);

            return WordbankAutoGenerateResult.Generated(
                normalized.Path, normalized.Metadata, _espeak.GeneratorVersion, generatedAt);
        }
        finally
        {
            if (!_options.KeepStagingFiles && File.Exists(stagingPath))
                File.Delete(stagingPath);
        }
    }

    private string ResolveStagingPath(string locale, string lemma)
    {
        var root = string.IsNullOrWhiteSpace(_options.StagingRoot)
            ? Path.Combine(Path.GetTempPath(), "soundscript-wordbank-staging")
            : _options.StagingRoot!;

        var dir = Path.Combine(root, locale);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, lemma + ".raw.wav");
    }

    /// <summary>
    /// Upserts the lemma entry in <c>lemmas.json</c> with generator provenance,
    /// preserving all existing entries verbatim, and returns the in-memory entry.
    /// </summary>
    private CorpusLemmaEntry PersistLemmaMetadata(
        string locale, string lemma, int? normalizerVersion, string generatedAt)
    {
        var audioRel = $"audio/{locale}/normalized/{lemma}.wav";
        var entry = new CorpusLemmaEntry
        {
            Lemma = lemma,
            Audio = audioRel,
            Generator = GeneratorName,
            GeneratorVersion = _espeak.GeneratorVersion,
            GeneratedAt = generatedAt,
            NormalizerVersion = normalizerVersion,
        };

        var lemmaFile = CorpusCatalog.ResolveLemmaFilePath(locale);
        if (lemmaFile is null)
            return entry;

        var root = LoadOrCreateLemmaDocument(lemmaFile, locale);
        var entries = root["entries"] as JsonArray;
        if (entries is null)
        {
            entries = new JsonArray();
            root["entries"] = entries;
        }

        var target = entries
            .OfType<JsonObject>()
            .FirstOrDefault(e => string.Equals(
                e["lemma"]?.GetValue<string>(), lemma, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            target = new JsonObject();
            entries.Add(target);
        }

        target["lemma"] = lemma;
        target["audio"] = audioRel;
        target["generator"] = GeneratorName;
        target["generatorVersion"] = _espeak.GeneratorVersion;
        target["generatedAt"] = generatedAt;
        if (normalizerVersion is not null)
            target["normalizerVersion"] = normalizerVersion.Value;

        var json = root.ToJsonString(JsonWriteOptions).Replace("\r\n", "\n") + "\n";
        var dir = Path.GetDirectoryName(lemmaFile);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(lemmaFile, json);

        return entry;
    }

    private static JsonObject LoadOrCreateLemmaDocument(string lemmaFile, string locale)
    {
        if (File.Exists(lemmaFile))
        {
            var parsed = JsonNode.Parse(File.ReadAllText(lemmaFile)) as JsonObject;
            if (parsed is not null)
                return parsed;
        }

        return new JsonObject
        {
            ["corpusId"] = CorpusCatalog.CorpusId,
            ["locale"] = locale,
            ["version"] = 0,
            ["status"] = "pilot",
            ["entries"] = new JsonArray(),
        };
    }

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };
}
