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
        new("visual-progress", "Progress indicator", "A growing bar, outlined rounded panel, and portable text label over piano.", "visual-progress.ssv"),
        new("visual-process", "Process diagram", "Two labelled panels connected by an animated arrow, with a line divider.", "visual-process.ssv"),
        new("visual-instructions", "Instruction sequence", "A rotating triangle and three timed text instructions, synchronized with piano.", "visual-instructions.ssv"),
        new("visual-status", "Status display", "A ring and circle indicator beside a rotating ellipse and text label.", "visual-status.ssv"),
    ];
}
