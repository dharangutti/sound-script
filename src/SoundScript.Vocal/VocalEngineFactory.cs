namespace SoundScript.Vocal;

public static class VocalEngineFactory
{
    public static IVocalEngine Create(string? engineName)
    {
        var normalized = engineName?.Trim().ToLowerInvariant() switch
        {
            null or "" => "composite",
            "espeak" or "espeak-ng" or "espeakng" => "espeak",
            "prosody" or "builtin" or "built-in" => "prosody",
            "wordbank" or "corpus" => "wordbank",
            "composite" or "default" => "composite",
            var other => other,
        };

        return normalized switch
        {
            "espeak" => new EspeakNgVocalEngine(),
            "prosody" => new ProsodyVocalEngine(),
            "wordbank" => new WordbankVocalEngine(),
            "composite" => new CompositeVocalEngine(),
            _ => throw new ArgumentException(
                $"Unknown vocal engine '{engineName}'. Supported: wordbank, composite, prosody, espeak.",
                nameof(engineName)),
        };
    }

    /// <summary>Default engine: wordbank corpus + G2P with espeak/prosody fallback per word.</summary>
    public static IVocalEngine CreateDefault() => new CompositeVocalEngine();
}
