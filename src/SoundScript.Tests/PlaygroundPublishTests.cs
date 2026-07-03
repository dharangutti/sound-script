using Xunit;

namespace SoundScript.Tests;

public class PlaygroundPublishTests
{
    private static readonly string[] RequiredPitchClasses =
        ["C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs", "A", "As", "B"];

    [Fact]
    public void PublishedArtifacts_ContainRequiredPlaygroundFiles()
    {
        var playgroundDir = GetPlaygroundPublishDir();
        Assert.True(File.Exists(Path.Combine(playgroundDir, "index.html")), "Missing index.html");
        Assert.True(File.Exists(Path.Combine(playgroundDir, "_framework", "blazor.webassembly.js")),
            "Missing _framework/blazor.webassembly.js");

        var samplesDir = Path.Combine(playgroundDir, "soundfont", "samples");
        Assert.True(Directory.Exists(samplesDir), "Missing soundfont/samples directory");

        foreach (var pitch in RequiredPitchClasses)
        {
            var samplePath = Path.Combine(samplesDir, $"{pitch}.wav");
            Assert.True(File.Exists(samplePath), $"Missing soundfont sample: {pitch}.wav");
            Assert.True(new FileInfo(samplePath).Length > 44, $"Invalid WAV file: {pitch}.wav");
        }
    }

    [Fact]
    public void SourceWwwroot_ContainsSoundfontSamplesForPublish()
    {
        var wwwrootDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SoundScript.Playground/wwwroot"));

        var samplesDir = Path.Combine(wwwrootDir, "soundfont", "samples");
        Assert.True(Directory.Exists(samplesDir), "Missing wwwroot soundfont/samples directory");

        foreach (var pitch in RequiredPitchClasses)
        {
            var samplePath = Path.Combine(samplesDir, $"{pitch}.wav");
            Assert.True(File.Exists(samplePath), $"Missing source soundfont sample: {pitch}.wav");
        }
    }

    private static string GetPlaygroundPublishDir() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/playground"));
}
