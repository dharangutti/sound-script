namespace SoundScript.Vocal;

/// <summary>Options passed to offline vocal engines.</summary>
public sealed class VocalEngineOptions
{
    /// <summary>Engine-specific voice id (e.g. eSpeak <c>en</c>).</summary>
    public string Voice { get; init; } = "en";

    /// <summary>Deterministic seed for the built-in prosody engine.</summary>
    public int Seed { get; init; } = 7;
}
