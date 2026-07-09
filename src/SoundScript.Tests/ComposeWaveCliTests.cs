using System.Diagnostics;
using SoundScript.Wave;
using Xunit;

namespace SoundScript.Tests;

public class ComposeWaveCliTests
{
    [Fact]
    public void Compose_WithWaveFlag_WritesWavWithoutMidiStep()
    {
        using var dir = new TempOutputDirectory();
        var wavPath = dir.FilePath("twinkle.wav");

        var (exitCode, stdout, _) = RunCli(
            $"compose \"Twinkle twinkle little star\" \"{wavPath}\" --wave");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(wavPath));
        Assert.True(new FileInfo(wavPath).Length > 44);
        Assert.Contains("SoundScript.Wave (no MIDI step)", stdout);
        Assert.DoesNotContain(".mid", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prosody_WithWaveFlag_WritesWavWithoutMidiStep()
    {
        using var dir = new TempOutputDirectory();
        var wavPath = dir.FilePath("twinkle-prosody.wav");

        var (exitCode, stdout, _) = RunCli(
            $"prosody \"Twinkle twinkle little star\" \"{wavPath}\" --wave");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(wavPath));
        Assert.Contains("word-level prosody", stdout);
        Assert.Contains("SoundScript.Wave (no MIDI step)", stdout);
    }

    [Fact]
    public void Compose_WaveAndAppend_AreMutuallyExclusive()
    {
        using var dir = new TempOutputDirectory();
        dir.Write("base.ss", """
            tempo 100
            track melody { C4 q }
            """);

        var (_, _, stderr) = RunCli(
            $"compose \"hello\" --wave --append \"{dir.FilePath("base.ss")}\"");

        Assert.Contains("--wave and --append cannot be combined", stderr);
    }

    [Fact]
    public void Compose_WaveOutput_IsDeterministic()
    {
        using var dir = new TempOutputDirectory();
        var a = dir.FilePath("a.wav");
        var b = dir.FilePath("b.wav");

        Assert.Equal(0, RunCli($"compose \"hello world\" \"{a}\" --wave").ExitCode);
        Assert.Equal(0, RunCli($"compose \"hello world\" \"{b}\" --wave").ExitCode);

        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
    }

    [Fact]
    public void Compose_Wave_MatchesDirectWaveRenderer()
    {
        using var dir = new TempOutputDirectory();
        var wavPath = dir.FilePath("cli.wav");
        const string text = "Twinkle twinkle little star";

        Assert.Equal(0, RunCli($"compose \"{text}\" \"{wavPath}\" --wave").ExitCode);

        var ast = SoundScript.Compose.PhonemeComposer.BuildAst(text);
        var expected = WaveRenderer.RenderToBytes(ast);

        Assert.Equal(expected, File.ReadAllBytes(wavPath));
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string arguments)
    {
        var cliDll = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../SoundScript.Cli/bin/Debug/net8.0/soundscript.dll"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{cliDll}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private sealed class TempOutputDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-wave-cli-" + Guid.NewGuid().ToString("N"));

        public TempOutputDirectory() => Directory.CreateDirectory(Root);

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Write(string fileName, string content) =>
            File.WriteAllText(FilePath(fileName), content);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
