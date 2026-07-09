namespace SoundScript.Wave;

/// <summary>Optional render-time inputs for SoundScript.Wave (V8 vocal stems / CLI overlays).</summary>
public sealed class WaveRenderOptions
{
    /// <summary>Directory containing the source script — used to resolve relative sample paths.</summary>
    public string? ScriptDirectory { get; init; }

    /// <summary>Additional sample overlays (e.g. CLI <c>--tts-dir</c>).</summary>
    public IReadOnlyList<Adapter.SampleOverlayRequest>? AdditionalSampleOverlays { get; init; }

    /// <summary>Additional mono overlays mixed before master effects (e.g. CLI <c>--vocal</c>).</summary>
    public IReadOnlyList<WaveExternalOverlay>? ExternalOverlays { get; init; }

    /// <summary>When true, missing sample files are skipped instead of failing (Playground).</summary>
    public bool SkipMissingSamples { get; init; }

    /// <summary>
    /// Skip synthetic <c>speak</c> phoneme tones — use with <see cref="AdditionalSampleOverlays"/>.
    /// Auto-enabled when additional overlays are present.
    /// </summary>
    public bool SuppressSyntheticSpeak { get; init; }
}

/// <summary>A pre-loaded mono buffer positioned on the timeline.</summary>
public sealed record WaveExternalOverlay(float[] Samples, double StartTimeSeconds, double Gain);

/// <summary>Resolves relative audio paths against a script directory.</summary>
public static class WavePathResolver
{
    public static string Resolve(string? scriptDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return Path.GetFullPath(relativePath);

        var baseDir = string.IsNullOrWhiteSpace(scriptDirectory)
            ? Directory.GetCurrentDirectory()
            : scriptDirectory;

        return Path.GetFullPath(Path.Combine(baseDir, relativePath));
    }
}
