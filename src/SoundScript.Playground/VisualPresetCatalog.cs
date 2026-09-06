namespace SoundScript.Playground;

public sealed record VisualPreset(string Key, string Title, string Description, string ExampleFile)
{
    public string Source
    {
        get
        {
            using var stream = typeof(VisualPresetCatalog).Assembly.GetManifestResourceStream($"VisualExamples.{ExampleFile}")
                ?? throw new InvalidOperationException($"Missing visual example: {ExampleFile}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}

public static class VisualPresetCatalog
{
    public static IReadOnlyList<VisualPreset> All { get; } =
    [
        new("visual-temporal", "Audio/Visual showcase", "A 12-second piano score with sequential cues, overlap, and fades.", "visual-temporal.ssv"),
        new("visual-motion", "Moving orb", "Position and radius animation with a rotating sparkle over piano.", "visual-motion.ssv"),
        new("visual-story", "Ready, Go, Done", "Named cards, a deliberate pause, resizing, and fades over a seven-second score.", "visual-story.ssv"),
        new("visual-overlays", "Layered product cue", "Overlapping card, orb, and star with millisecond placement and size animation.", "visual-overlays.ssv"),
    ];
}
