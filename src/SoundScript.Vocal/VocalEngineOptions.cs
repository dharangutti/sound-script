using SoundScript.Timbre;

namespace SoundScript.Vocal;

/// <summary>Options passed to offline vocal engines.</summary>
public sealed class VocalEngineOptions
{
    /// <summary>Engine-specific voice id (e.g. eSpeak <c>en</c>).</summary>
    public string Voice { get; init; } = "en";

    /// <summary>Wordbank locale for corpus lookup and G2P (defaults to active catalog locale).</summary>
    public string? Locale { get; init; }

    /// <summary>Deterministic seed for the built-in prosody engine.</summary>
    public int Seed { get; init; } = 7;

    /// <summary>Linear gain applied after peak normalization (default 1.0).</summary>
    public double OutputGain { get; init; } = 1.0;

    /// <summary>
    /// Optional word-level SoundCSS pronunciation rules (keyed by word,
    /// case-insensitive) parsed from a <c>--css</c> stylesheet. When set, each
    /// matching word stem is transformed via <see cref="SoundCssDspMapper"/> +
    /// the DSP renderer before mixing.
    /// </summary>
    public IReadOnlyDictionary<string, SoundCssPronunciation>? Pronunciations { get; init; }
}
