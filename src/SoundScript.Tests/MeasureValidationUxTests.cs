using System.Diagnostics;
using System.Text.RegularExpressions;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class MeasureValidationUxTests
{
    [Fact]
    public void MeasureWarnings_IncludeFileAndLine()
    {
        const string source = """
            time 4/4
            melody {
                C4 q E4 q G4 q |
                C4 h |
            }
            """;

        var interpreted = Interpreter.Interpret(
            new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse(),
            "script.ss");

        Assert.Contains(interpreted.Warnings, warning =>
            Regex.IsMatch(warning, @"script\.ss:\d+:") && warning.Contains("Measure 1 incomplete"));
        Assert.Contains(interpreted.Warnings, warning =>
            Regex.IsMatch(warning, @"script\.ss:\d+:") && warning.Contains("Measure 2 incomplete"));
    }

    [Fact]
    public void Cli_PrintsInterpretWarnings()
    {
        using var dir = new TempScriptDirectory();
        dir.Write("warn.ss", """
            time 4/4
            melody {
                C4 q E4 q G4 q |
            }
            """);

        var (exitCode, stdout, stderr) = RunCli(dir.FilePath("warn.ss"), dir.FilePath("out.mid"));

        Assert.Equal(0, exitCode);
        var combined = stdout + stderr;
        Assert.Matches(new Regex(@"warn\.ss:\d+:"), combined);
        Assert.Contains("Measure 1 incomplete", combined);
    }

    [Fact]
    public void Playground_ShowsWarningsInOutputPanel()
    {
        var razorPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SoundScript.Playground/Pages/Playground.razor"));
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SoundScript.Playground/Pages/Playground.razor.cs"));

        var razor = File.ReadAllText(razorPath);
        var code = File.ReadAllText(codePath);

        Assert.Contains("WarningMessages", razor, StringComparison.Ordinal);
        Assert.Contains("interpreted.Warnings", code, StringComparison.Ordinal);
        Assert.Contains("SourceDiagnostics", code, StringComparison.Ordinal);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string scriptPath, string outputPath)
    {
        var cliDll = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../SoundScript.Cli/bin/Debug/net8.0/soundscript.dll"));

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{cliDll}\" run \"{scriptPath}\" \"{outputPath}\"",
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

    private sealed class TempScriptDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-warn-" + Guid.NewGuid().ToString("N"));

        public TempScriptDirectory() => Directory.CreateDirectory(Root);

        public void Write(string fileName, string content) =>
            File.WriteAllText(Path.Combine(Root, fileName), content);

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
