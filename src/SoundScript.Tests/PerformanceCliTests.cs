using System.Diagnostics;
using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;
using Xunit;

namespace SoundScript.Tests;

public sealed class PerformanceCliTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "soundscript-progress-" + Guid.NewGuid().ToString("N"));

    public PerformanceCliTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string FilePath(string name) => Path.Combine(_root, name);

    private string Write(string name, string source)
    {
        var path = FilePath(name);
        File.WriteAllText(path, source);
        return path;
    }

    private static string CliDll => TestBuildPaths.CliDll;
    private static string FakeFfmpeg => TestBuildPaths.ProcessFixture;

    private (int ExitCode, string StdOut, string StdErr) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(CliDll);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000)) { process.Kill(true); throw new TimeoutException("CLI process timed out."); }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    [Fact]
    public void VerboseValidationUsesStderrAndJsonRemainsClean()
    {
        var input = Write("scene.ssv", "visual \"title\" for 1s");
        var concise = Run("validate", input);
        Assert.Equal(0, concise.ExitCode);
        Assert.DoesNotContain("Compiling...", concise.StdErr, StringComparison.Ordinal);

        var verbose = Run("validate", input, "--verbose");
        Assert.Equal(0, verbose.ExitCode);
        Assert.Contains("Compiling...", verbose.StdErr, StringComparison.Ordinal);
        Assert.Contains("Compiling completed", verbose.StdErr, StringComparison.Ordinal);

        var json = Run("validate", input, "--json", "--verbose");
        Assert.Equal(0, json.ExitCode);
        using var document = JsonDocument.Parse(json.StdOut);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("Compiling...", json.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseVideoReportsStagesAndBoundedProgress()
    {
        var input = Write("scene.ssv", "visual \"title\" for 1s");
        var output = FilePath("scene.webm");
        var result = Run("video", input, "--out", output, "--width", "32", "--height", "24", "--ffmpeg", FakeFfmpeg, "--jobs", "4", "--verbose");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Preparing media timeline...", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Rendering frames...", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Frames rendered: 30/30 100%", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Encoding WebM with FFmpeg...", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Verifying output...", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Total elapsed:", result.StdErr, StringComparison.Ordinal);
        Assert.InRange(result.StdErr.Split('\n').Count(line => line.Contains("Frames rendered:", StringComparison.Ordinal)), 2, 21);
        Assert.DoesNotContain("Frames rendered: 1/30", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("{", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonVideoPreflightWithVerboseKeepsStdoutMachineReadable()
    {
        var input = Write("scene.ssv", "visual \"title\" for 1s");
        var output = FilePath("scene.webm");
        var result = Run("video", input, "--out", output, "--check", "--json", "--verbose", "--ffmpeg", FakeFfmpeg);
        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StdOut);
        Assert.Equal("video", document.RootElement.GetProperty("command").GetString());
        Assert.Contains("Preparing media timeline...", result.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void SingleAndParallelFrameGenerationAreByteIdentical()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "../../../../../examples/visual-temporal.ssv");
        var timeline = VisualInterpreter.Interpret(ProgramLoader.Load(source).Program);
        var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline, new(30)));
        var one = FilePath("one"); var many = FilePath("many");
        var first = new Progress<int>(); var second = new Progress<int>();
        TemporalVideoFrameRenderer.WritePpmFrames(plan, one, 32, 24, first, jobs: 1);
        TemporalVideoFrameRenderer.WritePpmFrames(plan, many, 32, 24, second, jobs: 4);
        var firstFrames = Directory.GetFiles(one, "frame-*.ppm").OrderBy(path => path).ToArray();
        var secondFrames = Directory.GetFiles(many, "frame-*.ppm").OrderBy(path => path).ToArray();
        Assert.Equal(firstFrames.Length, secondFrames.Length);
        for (var index = 0; index < firstFrames.Length; index++) Assert.Equal(File.ReadAllBytes(firstFrames[index]), File.ReadAllBytes(secondFrames[index]));
    }

    [Fact]
    public void CancellationStopsFrameGenerationBeforeFinalOutput()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "../../../../../examples/visual-temporal.ssv");
        var timeline = VisualInterpreter.Interpret(ProgramLoader.Load(source).Program);
        var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline, new(60)));
        var directory = FilePath("cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => TemporalVideoFrameRenderer.WritePpmFrames(plan, directory, 32, 24, cancellationToken: cancellation.Token, jobs: 4));
        Assert.True(!Directory.Exists(directory) || Directory.GetFiles(directory).Length < plan.Samples.Count);
    }
}
