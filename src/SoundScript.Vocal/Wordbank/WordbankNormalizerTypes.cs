using System.Text.Json.Serialization;

namespace SoundScript.Vocal.Wordbank;

/// <summary>
/// Deterministic tuning knobs for <see cref="WordbankNormalizer"/>. All values
/// participate in output determinism: two runs with equal options and source
/// audio produce byte-identical WAVs.
/// </summary>
public sealed record WordbankNormalizerOptions
{
    /// <summary>Peak ceiling in dBFS the normalized signal must not exceed.</summary>
    public double TargetPeakDbFs { get; init; } = -1.0;

    /// <summary>Target loudness in dBFS RMS, applied under the peak ceiling.</summary>
    public double TargetRmsDbFs { get; init; } = -20.0;

    /// <summary>Amplitude in dBFS below which edge samples count as silence for trimming.</summary>
    public double SilenceThresholdDbFs { get; init; } = -40.0;

    /// <summary>Milliseconds of audio retained on each side of the trimmed region (anti-click).</summary>
    public double EdgePaddingMs { get; init; } = 5.0;

    /// <summary>
    /// When true, applies the per-lemma <c>pitchSemitones</c> from the corpus
    /// entry as a small pitch-centering shift. Disable for raw normalization.
    /// </summary>
    public bool ApplyPitchCentering { get; init; } = true;

    public static WordbankNormalizerOptions Default { get; } = new();
}

/// <summary>
/// Metadata extracted during normalization. These keys are a normalization-only
/// sidecar and intentionally do not overlap with the wordbank lemma schema
/// (lemma/license/source/attribution/audio/trim*/gain/pitchSemitones).
/// </summary>
public sealed record WordbankNormalizedMetadata
{
    [JsonPropertyName("normalized")]
    public bool Normalized { get; init; } = true;

    [JsonPropertyName("basePitchHz")]
    public double BasePitchHz { get; init; }

    [JsonPropertyName("durationMs")]
    public double DurationMs { get; init; }

    [JsonPropertyName("energyRMS")]
    public double EnergyRms { get; init; }

    [JsonPropertyName("normalizerVersion")]
    public int NormalizerVersion { get; init; }
}

/// <summary>Outcome of a <see cref="WordbankNormalizer.Normalize"/> call.</summary>
public sealed record WordbankNormalizeResult
{
    /// <summary>True when a canonical WAV + metadata were written.</summary>
    public bool Success { get; init; }

    /// <summary>True when the source corpus audio was absent (see <see cref="Reason"/>).</summary>
    public bool Missing { get; init; }

    /// <summary>On success, the on-disk path of the normalized WAV.</summary>
    public string? Path { get; init; }

    /// <summary>On success, the extracted metadata.</summary>
    public WordbankNormalizedMetadata? Metadata { get; init; }

    /// <summary>Human-readable explanation when <see cref="Success"/> is false.</summary>
    public string? Reason { get; init; }

    internal static WordbankNormalizeResult SuccessResult(string path, WordbankNormalizedMetadata metadata) =>
        new() { Success = true, Path = path, Metadata = metadata };

    internal static WordbankNormalizeResult MissingResult(string reason) =>
        new() { Success = false, Missing = true, Reason = reason };
}
