namespace SoundScript.Playground;

/// <summary>One source of truth: the Playground embeds the regression/example files.</summary>
public static class PerformanceExamples
{
    public static IReadOnlyList<(string Key, string Title)> All { get; } =
    [
        ("performance-harbor", "Harbor Lights — cinematic ballad"),
        ("performance-bells", "Jingle Bells — articulated refrain"),
        ("performance-ode", "Ode to Joy — lyrical violin"),
        ("performance-clockwork", "Clockwork Garden — fast staccato"),
        ("performance-lanterns", "Floating Lanterns — sustained layers")
    ];

    public static string Source(string key)
    {
        if (!All.Any(x => x.Key == key)) throw new ArgumentException("Unknown performance example.", nameof(key));
        using var stream = typeof(PerformanceExamples).Assembly.GetManifestResourceStream($"PerformanceExamples.{key}.ss")
            ?? throw new InvalidOperationException($"Missing embedded example: {key}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
