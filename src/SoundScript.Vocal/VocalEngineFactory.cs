namespace SoundScript.Vocal;

public static class VocalEngineFactory
{
    public static IVocalEngine Create(string? engineName)
    {
        var normalized = engineName?.Trim().ToLowerInvariant() switch
        {
            null or "" => "prosody",
            "espeak" or "espeak-ng" or "espeakng" => "espeak",
            "prosody" or "builtin" or "built-in" => "prosody",
            var other => other,
        };

        return normalized switch
        {
            "espeak" => new EspeakNgVocalEngine(),
            "prosody" => new ProsodyVocalEngine(),
            _ => throw new ArgumentException(
                $"Unknown vocal engine '{engineName}'. Supported: espeak, prosody.",
                nameof(engineName)),
        };
    }

    /// <summary>Default engine for CLI when --engine is omitted: espeak if installed, else prosody.</summary>
    public static IVocalEngine CreateDefault()
    {
        return EspeakNgVocalEngine.ResolveExecutable() is not null
            ? new EspeakNgVocalEngine()
            : new ProsodyVocalEngine();
    }
}
