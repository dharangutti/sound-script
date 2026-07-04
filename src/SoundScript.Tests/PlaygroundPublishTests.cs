using Xunit;

namespace SoundScript.Tests;

public class PlaygroundPublishTests
{
    private static readonly string[] RequiredPitchClasses =
        ["C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs", "A", "As", "B"];

    private static readonly int[] RequiredPrograms = [0, 19, 24, 32, 40, 42, 56, 73, 80];

    [Fact]
    public void PublishedArtifacts_ContainRequiredPlaygroundFiles()
    {
        var playgroundDir = GetPlaygroundPublishDir();
        Assert.True(File.Exists(Path.Combine(playgroundDir, "index.html")), "Missing index.html");
        Assert.True(File.Exists(Path.Combine(playgroundDir, "_framework", "blazor.webassembly.js")),
            "Missing _framework/blazor.webassembly.js");

        AssertSoundfontSamples(Path.Combine(playgroundDir, "soundfont", "samples"));
    }

    [Fact]
    public void SourceWwwroot_ContainsSoundfontSamplesForPublish()
    {
        var wwwrootDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SoundScript.Playground/wwwroot"));

        AssertSoundfontSamples(Path.Combine(wwwrootDir, "soundfont", "samples"));
    }

    private static void AssertSoundfontSamples(string samplesRoot)
    {
        Assert.True(Directory.Exists(samplesRoot), "Missing soundfont/samples directory");

        foreach (var program in RequiredPrograms)
        {
            var programDir = Path.Combine(samplesRoot, program.ToString());
            Assert.True(Directory.Exists(programDir), $"Missing soundfont/samples/{program} directory");

            foreach (var pitch in RequiredPitchClasses)
            {
                var samplePath = Path.Combine(programDir, $"{pitch}.wav");
                Assert.True(File.Exists(samplePath), $"Missing soundfont sample: {program}/{pitch}.wav");
                Assert.True(new FileInfo(samplePath).Length > 44, $"Invalid WAV file: {program}/{pitch}.wav");
            }
        }
    }

    private static string GetPlaygroundPublishDir() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/playground"));
}
