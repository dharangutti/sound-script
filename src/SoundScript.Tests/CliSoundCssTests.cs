using System.Diagnostics;
using Xunit;

namespace SoundScript.Tests;

/// <summary>
/// Regression: <c>wave --css</c> must apply word-level SoundCSS to speak lines
/// even without an explicit <c>--offline-tts</c> (previously it was silently
/// ignored, so changing a word rule had no effect and CLI output was
/// inconsistent depending on flag combination).
/// </summary>
public class CliSoundCssTests
{
    [Fact]
    public void Wave_WithCssButNoOfflineTts_AppliesWordRules()
    {
        using var dir = new TempDir();
        var script = dir.File("song.ssw");
        File.WriteAllText(script,
            """
            tempo 120
            speak "hello welcome" seed=7
            """);

        var maleCss = dir.File("male.ssc");
        File.WriteAllText(maleCss,
            """
            "hello"   { style: sing; gender: male; }
            "welcome" { style: sing; gender: male; }
            """);

        var femaleCss = dir.File("female.ssc");
        File.WriteAllText(femaleCss,
            """
            "hello"   { style: sing; gender: female; }
            "welcome" { style: sing; gender: female; }
            """);

        var maleOut = dir.File("male.wav");
        var femaleOut = dir.File("female.wav");

        var male = RunCli($"wave \"{script}\" \"{maleOut}\" --css \"{maleCss}\" --offline-tts-dir \"{dir.File("m")}\" --locale en");
        var female = RunCli($"wave \"{script}\" \"{femaleOut}\" --css \"{femaleCss}\" --offline-tts-dir \"{dir.File("f")}\" --locale en");

        Assert.Equal(0, male.ExitCode);
        Assert.Equal(0, female.ExitCode);
        Assert.True(File.Exists(maleOut));
        Assert.True(File.Exists(femaleOut));

        // Different gender rules must yield different audio through wave --css.
        Assert.NotEqual(File.ReadAllBytes(maleOut), File.ReadAllBytes(femaleOut));
    }

    [Fact]
    public void Wave_WithCss_IsDeterministicAcrossRuns()
    {
        using var dir = new TempDir();
        var script = dir.File("song.ssw");
        File.WriteAllText(script,
            """
            tempo 120
            speak "hello welcome" seed=7
            """);
        var css = dir.File("style.ssc");
        File.WriteAllText(css, "\"hello\" { style: sing; pitch: +3; }");

        var a = dir.File("a.wav");
        var b = dir.File("b.wav");
        Assert.Equal(0, RunCli($"wave \"{script}\" \"{a}\" --css \"{css}\" --offline-tts-dir \"{dir.File("sa")}\" --locale en").ExitCode);
        Assert.Equal(0, RunCli($"wave \"{script}\" \"{b}\" --css \"{css}\" --offline-tts-dir \"{dir.File("sb")}\" --locale en").ExitCode);

        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
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
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private sealed class TempDir : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ss-cli-css-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(_root);
        public string File(string name) => Path.Combine(_root, name);
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
