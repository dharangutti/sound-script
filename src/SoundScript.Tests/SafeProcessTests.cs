using System.Diagnostics;
using System.Text.Json;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Vocal;
using Xunit;

namespace SoundScript.Tests;

public sealed class SafeProcessTests
{
    [Fact]
    public async Task ArgumentsArePassedLiterallyWithoutShellExpansion()
    {
        string[] args = ["with space", "quoted\"text", ";echo injected", "$(echo secret)", "--option"];
        var result = await SafeProcess.RunAsync(TestBuildPaths.ProcessFixture, ["--process-test", "echo", ..args], TimeSpan.FromSeconds(15));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(args, JsonSerializer.Deserialize<string[]>(result.StandardOutput));
    }

    [Fact]
    public async Task DrainsBothPipesAndPreservesNonzeroExit()
    {
        var result = await SafeProcess.RunAsync(TestBuildPaths.ProcessFixture, ["--process-test", "flood"], TimeSpan.FromSeconds(15));
        Assert.Equal(7, result.ExitCode);
        Assert.Equal(262144, result.StandardOutput.Length);
        Assert.Equal(262144, result.StandardError.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndCancellationTerminateChildTree(bool cancel)
    {
        var pidFile = Path.Combine(Path.GetTempPath(), "soundscript-process-" + Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        var run = SafeProcess.RunAsync(TestBuildPaths.ProcessFixture, ["--process-test", "tree", pidFile],
            TimeSpan.FromSeconds(cancel ? 30 : 5), cancellation.Token);
        try
        {
            var ready = Stopwatch.StartNew();
            while (!File.Exists(pidFile) && ready.Elapsed < TimeSpan.FromSeconds(4)) await Task.Delay(25);
            Assert.True(File.Exists(pidFile), "Process tree did not start before deadline.");
            var pid = int.Parse(File.ReadAllText(pidFile));
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            }
            else Assert.Contains("timed out", (await Assert.ThrowsAsync<TimeoutException>(() => run)).Message);
            try { using var child = Process.GetProcessById(pid); Assert.True(child.HasExited || child.WaitForExit(5000)); }
            catch (ArgumentException) { } // Process already reaped.
        }
        finally
        {
            cancellation.Cancel();
            try { await run; } catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
            File.Delete(pidFile);
        }
    }

    [Fact]
    public async Task PreCanceledRunDoesNotStartMissingExecutable()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SafeProcess.RunAsync("missing-executable", [], TimeSpan.FromSeconds(1), cancellation.Token));
    }

    [Fact]
    public void FfmpegRetainsExitCodeAndDiagnostic()
    {
        var error = Assert.Throws<ExportException>(() => FfmpegWebmExporter.RunMediaProcess(TestBuildPaths.ProcessFixture, ["--process-test", "flood"]));
        Assert.Contains("code 7", error.Message);
    }

    [Fact]
    public void EspeakHonorsCancellationBeforeSynthesis()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var options = new VocalEngineOptions { CancellationToken = cancellation.Token };
        Assert.ThrowsAny<OperationCanceledException>(() => new EspeakNgVocalEngine().Synthesize("hello", "unused.wav", options));
    }
}
