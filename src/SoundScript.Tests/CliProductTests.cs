using System.Diagnostics;
using System.Text.Json;
using SoundScript.Cli;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Wave;
using Xunit;

namespace SoundScript.Tests;

public sealed class CliProductTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "soundscript cli tests " + Guid.NewGuid().ToString("N"));
    public CliProductTests() => Directory.CreateDirectory(root);
    private string FilePath(string name) => Path.Combine(root, name);
    private string Source(string source, string name = "input.ss") { var path = FilePath(name); File.WriteAllText(path, source); return path; }
    public void Dispose() => Directory.Delete(root, true);
    private static string BuildPath(string project, string file) => TestBuildPaths.ProjectOutput(project, file);
    private static string FakeFfmpeg => BuildPath("SoundScript.Cli.TestFfmpeg", "SoundScript.Cli.TestFfmpeg" + (OperatingSystem.IsWindows() ? ".exe" : ""));

    private (int Code, string Out, string Error) Run(params string[] args) => RunWithEnvironment(null, args);
    private (int Code, string Out, string Error) RunWithEnvironment(string? mode, params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(BuildPath("SoundScript.Cli", "soundscript.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment.Remove("WORDBANK_DIR");
        if (mode is not null) start.Environment["SS_TEST_FFMPEG_MODE"] = mode;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("CLI child exceeded one minute."); }
        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    [Theory]
    [InlineData("tempo 120\ntrack tune { C4 q }", "input.ss")]
    [InlineData("tempo 120\nspeak \"hello\" seed=7", "input.ssw")]
    [InlineData("visual \"title\" for 1s", "input.ssv")]
    [InlineData("aa { formant1: 700Hz; }\n\"hello\" { style: sing; }", "input.ssc")]
    public void ValidateSupportsEverySourceTypeWithoutGeneratingMedia(string source, string name)
    {
        var input = Source(source, name); var result = Run("validate", input, "--json");
        Assert.True(result.Code == 0, result.Out + result.Error);
        using var json = JsonDocument.Parse(result.Out);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(VersionInfo.Number, json.RootElement.GetProperty("soundScriptVersion").GetString());
        Assert.Empty(result.Error); Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void SyntaxFailureHasExactImportedLocationAndSingleDiagnostic()
    {
        var imported = Source("tempo 120\n  nonsense\n", "library.ss");
        var input = Source("import \"library.ss\"\ntrack tune { C4 q }");
        var result = Run("validate", input, "--json");
        Assert.Equal(1, result.Code);
        using var json = JsonDocument.Parse(result.Out); var diagnostics = json.RootElement.GetProperty("diagnostics");
        Assert.Equal(1, diagnostics.GetArrayLength()); var d = diagnostics[0];
        Assert.Equal(imported, d.GetProperty("file").GetString());
        Assert.Equal(2, d.GetProperty("line").GetInt32()); Assert.Equal(3, d.GetProperty("column").GetInt32());
        Assert.Equal("error", d.GetProperty("severity").GetString());
    }

    [Fact]
    public void SemanticFailurePointsToNestedImportedPlay()
    {
        var imported = Source("block broken {\n  play missing\n}\n", "library.ss");
        var result = Run("validate", Source("import \"library.ss\"\ntrack tune { play broken }"), "--json");
        Assert.Equal(1, result.Code);
        using var json = JsonDocument.Parse(result.Out); var d = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(imported, d.GetProperty("file").GetString()); Assert.Equal(2, d.GetProperty("line").GetInt32()); Assert.Equal(3, d.GetProperty("column").GetInt32());
        Assert.Equal("SS2001", d.GetProperty("code").GetString());
    }

    [Fact]
    public void WarningOnlyValidationSucceedsAndSeparatesStreams()
    {
        var input = Source("tempo 120\ntrack tune { C4 q }\nvisual \"title\" for 2s", "scene.ssv");
        var result = Run("validate", input); Assert.Equal(0, result.Code);
        Assert.Contains("Valid:", result.Out); Assert.DoesNotContain("SS2104", result.Out); Assert.Contains("warning SS2104", result.Error);
        var structured = Run("validate", input, "--json"); Assert.Empty(structured.Error);
        using var json = JsonDocument.Parse(structured.Out);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("diagnostics").EnumerateArray(), d => d.GetProperty("code").GetString() == "SS2104");
    }

    [Fact]
    public void ImportedMeasureWarningRetainsFileAndBarColumn()
    {
        var library = Source("track tune {\n  C4 q |\n}", "library.ss");
        var result = Run("validate", Source("time 4/4\nimport \"library.ss\""), "--json");
        Assert.Equal(0, result.Code); using var json = JsonDocument.Parse(result.Out);
        var d = json.RootElement.GetProperty("diagnostics").EnumerateArray().Single(d => d.GetProperty("message").GetString()!.Contains("Measure 1 incomplete"));
        Assert.Equal(library, d.GetProperty("file").GetString()); Assert.Equal(2, d.GetProperty("line").GetInt32()); Assert.Equal(8, d.GetProperty("column").GetInt32());
    }

    [Fact]
    public void InspectHumanAndJsonIncludeResolvedVisualState()
    {
        var input = Source("tempo 90\ntrack tune { C4 q }\nvisual \"title\" for 2s", "scene.ssv");
        var human = Run("inspect", input); Assert.Equal(0, human.Code); Assert.Contains("Tempo: 90 BPM", human.Out); Assert.Contains("Visual duration: 2", human.Out);
        var result = Run("inspect", input, "--at=1.5", "--json"); Assert.Equal(0, result.Code);
        using var json = JsonDocument.Parse(result.Out); var m = json.RootElement.GetProperty("metadata");
        Assert.Equal(1, m.GetProperty("visualCount").GetInt32()); Assert.Equal(1, m.GetProperty("noteCount").GetInt32());
        Assert.Equal("title", json.RootElement.GetProperty("results").GetProperty("state").GetProperty("elements")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void WaveMeasuredDurationMatchesRenderedLengthWithDelay()
    {
        var input = Source("tempo 120\nC4 q\neffect delay time=0.1 feedback=0.3 mix=0.4", "notes.ssw");
        var measured = SourceAnalysis.Load(input).Metadata.AudioDurationSeconds;
        var output = FilePath("notes.wav"); WaveRenderer.Render(ProgramLoader.Load(input).Program, output);
        Assert.Equal(SourceAnalysis.ReadWavDuration(output), measured);
    }

    [Theory]
    [InlineData("--streo")]
    [InlineData("--mystery")]
    [InlineData("--out")]
    [InlineData("--fps=abc")]
    public void BadOptionsFailAsUsage(string option)
    {
        var result = Run("wave", Source("C4 q"), option);
        Assert.Equal(2, result.Code); Assert.Contains("SS0002", result.Error); Assert.Empty(result.Out);
    }

    [Fact]
    public void MissingValueDuplicateAliasesAndConflictingOutputsFail()
    {
        var input = Source("C4 q");
        Assert.Equal(2, Run("wave", input, "--css", "--stereo").Code);
        Assert.Equal(2, Run("wave", input, "--out", "a.wav", "-o", "b.wav").Code);
        Assert.Equal(2, Run("wave", input, "a.wav", "--out", "b.wav").Code);
        Assert.Equal(2, Run("compose", "hello", "--wave", "--append", input).Code);
        Assert.Equal(2, Run("wordbank", "normalize", "hello", "--all").Code);
    }

    [Fact]
    public void MissingInputAndInvalidOutputMapToEnvironment()
    {
        var missing = Run("validate", FilePath("absent.ss"), "--json"); Assert.Equal(3, missing.Code);
        using var json = JsonDocument.Parse(missing.Out); Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        var input = Source("C4 q"); Assert.Equal(3, Run("wave", input, "--out", root).Code);
        var parent = Source("file", "parent"); Assert.Equal(3, Run("wave", input, "--out", Path.Combine(parent, "bad.wav")).Code);
        Assert.Equal("C4 q", File.ReadAllText(input));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("help")]
    [InlineData("validate", "--help")]
    [InlineData("help", "wave")]
    [InlineData("vocal", "--help")]
    [InlineData("help", "vocal", "generate")]
    public void HelpSucceeds(params string[] args)
    { var result = Run(args); Assert.Equal(0, result.Code); Assert.Contains("soundscript", result.Out); Assert.Empty(result.Error); }

    [Fact]
    public void VersionIsMachineFriendlyAndMatchesRelease()
    { var result = Run("--version"); Assert.Equal(0, result.Code); Assert.Equal(VersionInfo.Number, result.Out.Trim()); Assert.Equal("13.0.2", result.Out.Trim()); }

    [Fact]
    public void JsonUsageFailureIsOnlyJson()
    {
        var result = Run("validate", Source("C4 q"), "--json", "--typo"); Assert.Equal(2, result.Code); Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Out); Assert.Equal("validate", json.RootElement.GetProperty("command").GetString());
    }

    [Fact]
    public void PreflightRequiresFfmpegAndEncodersWithoutWritingOutputs()
    {
        var input = Source("visual \"title\" for 0.1s", "scene.ssv"); var output = FilePath("clip.webm");
        Assert.Equal(3, Run("video", input, "--out", output, "--check", "--ffmpeg", FilePath("absent-ffmpeg")).Code);
        Assert.Equal(3, RunWithEnvironment("missing-codec", "video", input, "--out", output, "--check", "--ffmpeg", FakeFfmpeg).Code);
        var success = Run("video", input, "--out", output, "--check", "--json", "--ffmpeg", FakeFfmpeg);
        Assert.True(success.Code == 0, success.Out + success.Error);
        using var json = JsonDocument.Parse(success.Out); Assert.Equal(3, json.RootElement.GetProperty("results").GetProperty("estimatedFrameCount").GetInt32());
        Assert.False(File.Exists(output)); Assert.Single(Directory.GetFiles(root));
    }

    [Theory]
    [InlineData("encode-failure")]
    [InlineData("decode-failure")]
    public void VideoFailuresPreserveExistingFinalAndCleanStaging(string mode)
    {
        var input = Source("visual \"title\" for 0.1s", "scene.ssv"); var output = Source("old output", "clip.webm");
        var result = RunWithEnvironment(mode, "video", input, "--out", output, "--width", "32", "--height", "24", "--ffmpeg", FakeFfmpeg);
        Assert.Equal(4, result.Code); Assert.Equal("old output", File.ReadAllText(output)); Assert.Empty(Directory.GetFiles(root, ".soundscript-*"));
        File.Delete(output);
        result = RunWithEnvironment(mode, "video", input, "--out", output, "--width", "32", "--height", "24", "--ffmpeg", FakeFfmpeg);
        Assert.Equal(4, result.Code); Assert.False(File.Exists(output));
    }

    [Fact]
    public void SuccessfulAtomicVideoReplacesOnlyAfterVerification()
    {
        var input = Source("visual \"title\" for 0.1s", "scene.ssv"); var output = Source("old output", "clip.webm");
        var result = Run("video", input, "--out", output, "--width", "32", "--height", "24", "--ffmpeg", FakeFfmpeg);
        Assert.True(result.Code == 0, result.Error); Assert.Equal("encoded test container", File.ReadAllText(output)); Assert.Empty(Directory.GetFiles(root, ".soundscript-*"));
    }

    [Fact]
    public void PositionalAndExplicitOutputAreByteIdentical()
    {
        var input = Source("tempo 120\ntrack tune { C4 q E4 q }");
        foreach (var command in new[] { "run", "wave" })
        {
            var extension = command == "run" ? ".mid" : ".wav"; var a = FilePath("positional" + extension); var b = FilePath("explicit" + extension);
            Assert.Equal(0, Run(command, input, a).Code); Assert.Equal(0, Run(command, input, "--out=" + b).Code);
            Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
        }
    }

    [Fact]
    public void AtomicVerificationFailureDoesNotCommitOrHideOriginal()
    {
        var output = Source("old", "output.wav");
        var ex = Assert.Throws<ExportException>(() => AtomicOutput.Write(output, p => File.WriteAllText(p, "partial"), _ => throw new InvalidDataException("decode failed")));
        Assert.Contains("decode failed", ex.Message); Assert.Equal("old", File.ReadAllText(output)); Assert.Empty(Directory.GetFiles(root, ".soundscript-*"));
    }

    [Fact]
    public void RecursiveWaveBlocksFailWithoutCrashing()
    { Assert.Equal(1, Run("validate", Source("block recursive { play recursive }\ntrack tune { play recursive }", "loop.ssw")).Code); }

    [Fact]
    public void SoundCssFailureIncludesPropertyLocation()
    {
        var result = Run("validate", Source("\"hello\" {\n  gender: unknown;\n}", "style.ssc"), "--json"); Assert.Equal(1, result.Code);
        using var json = JsonDocument.Parse(result.Out); var d = json.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(2, d.GetProperty("line").GetInt32()); Assert.Equal(3, d.GetProperty("column").GetInt32());
    }
}
