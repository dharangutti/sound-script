namespace SoundScript.Tests;

internal static class ExampleTestHelpers
{
    /// <summary>
    /// Example scripts that are valid on the wave backend but contain directives
    /// the MIDI interpreter rejects (<c>speak</c>, named <c>humanize</c>, etc.).
    /// </summary>
    private static readonly HashSet<string> MidiSkippedExamples = new(StringComparer.OrdinalIgnoreCase)
    {
        "speech-only-wave.ss",
    };

    internal static IEnumerable<string> EnumerateMidiCompatibleExamples()
    {
        var exampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples"));
        foreach (var path in Directory.GetFiles(exampleDir, "*.ss"))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith("-lib.ss", StringComparison.OrdinalIgnoreCase))
                continue;
            if (MidiSkippedExamples.Contains(name))
                continue;

            yield return path;
        }
    }
}
