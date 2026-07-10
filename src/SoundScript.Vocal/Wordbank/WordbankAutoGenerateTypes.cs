namespace SoundScript.Vocal.Wordbank;

/// <summary>Configuration for <see cref="WordbankAutoGenerate"/>.</summary>
public sealed record WordbankAutoGenerateOptions
{
    /// <summary>eSpeak voice id. When empty, the locale code is used as the voice.</summary>
    public string? Voice { get; init; }

    /// <summary>Directory for raw eSpeak staging WAVs. Defaults to a temp folder.</summary>
    public string? StagingRoot { get; init; }

    /// <summary>When true, retains the raw staging WAV after normalization.</summary>
    public bool KeepStagingFiles { get; init; }

    /// <summary>Normalization tuning passed through to <see cref="WordbankNormalizer"/>.</summary>
    public WordbankNormalizerOptions? NormalizerOptions { get; init; }

    public static WordbankAutoGenerateOptions Default { get; } = new();
}

/// <summary>Outcome classification for <see cref="WordbankAutoGenerate.EnsureLemma"/>.</summary>
public enum WordbankAutoGenerateStatus
{
    /// <summary>A normalized WAV already existed; nothing was generated.</summary>
    AlreadyPresent,

    /// <summary>A normalized WAV was synthesized and persisted this call.</summary>
    Generated,

    /// <summary>The lemma was missing and auto-generation was disabled.</summary>
    MissingGenerationDisabled,

    /// <summary>Auto-generation was requested but eSpeak is unavailable.</summary>
    GeneratorUnavailable,

    /// <summary>Generation was attempted but failed.</summary>
    Failed,
}

/// <summary>Result of an <see cref="WordbankAutoGenerate.EnsureLemma"/> call.</summary>
public sealed record WordbankAutoGenerateResult
{
    public WordbankAutoGenerateStatus Status { get; init; }

    /// <summary>True when the lemma now resolves to a normalized WAV on disk.</summary>
    public bool Resolved => Status is WordbankAutoGenerateStatus.AlreadyPresent or WordbankAutoGenerateStatus.Generated;

    /// <summary>Path to the normalized WAV when resolved.</summary>
    public string? Path { get; init; }

    /// <summary>Extracted audio metadata when a WAV was generated.</summary>
    public WordbankNormalizedMetadata? Metadata { get; init; }

    /// <summary>Generator tool version recorded on the lemma (when generated).</summary>
    public string? GeneratorVersion { get; init; }

    /// <summary>UTC generation timestamp recorded on the lemma (when generated).</summary>
    public string? GeneratedAt { get; init; }

    /// <summary>Human-readable explanation for non-resolved outcomes.</summary>
    public string? Reason { get; init; }

    internal static WordbankAutoGenerateResult AlreadyPresent(string path) =>
        new() { Status = WordbankAutoGenerateStatus.AlreadyPresent, Path = path };

    internal static WordbankAutoGenerateResult Generated(
        string path, WordbankNormalizedMetadata metadata, string generatorVersion, string generatedAt) =>
        new()
        {
            Status = WordbankAutoGenerateStatus.Generated,
            Path = path,
            Metadata = metadata,
            GeneratorVersion = generatorVersion,
            GeneratedAt = generatedAt,
        };

    internal static WordbankAutoGenerateResult MissingDisabled(string reason) =>
        new() { Status = WordbankAutoGenerateStatus.MissingGenerationDisabled, Reason = reason };

    internal static WordbankAutoGenerateResult GeneratorUnavailable(string reason) =>
        new() { Status = WordbankAutoGenerateStatus.GeneratorUnavailable, Reason = reason };

    internal static WordbankAutoGenerateResult Failed(string reason) =>
        new() { Status = WordbankAutoGenerateStatus.Failed, Reason = reason };
}
