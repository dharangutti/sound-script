using System.Diagnostics;
using System.Text.Json;
using SoundScript.Core;
using Xunit;

namespace SoundScript.Tests;

public sealed class RuntimeCliTests : IDisposable
{
    private const string SourceText = """
        param intensity = 0.25
        param xpos = 200
        perform expressive
        tempo 120
        track cue { gain intensity C4 q E4 q G4 h }
        visual "indicator" for 4s { shape circle set x xpos set opacity intensity }
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "soundscript runtime cli " + Guid.NewGuid().ToString("N"));

    public RuntimeCliTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void InspectRuntimeParametersReportsSchemaAndCurrentValuesAsJson()
    {
        var input = Write("monitor.ss", SourceText);
        var result = Run("inspect", input, "--params", "--json", "--param", "intensity=0.8", "--param=xpos=900");

        Assert.True(result.Code == 0, result.Out + result.Error);
        Assert.Empty(result.Error);
        using var json = JsonDocument.Parse(result.Out);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var parameters = json.RootElement.GetProperty("results").GetProperty("parameters");
        Assert.Equal(2, parameters.GetArrayLength());
        Assert.Equal("intensity", parameters[0].GetProperty("name").GetString());
        Assert.Equal("decimal", parameters[0].GetProperty("type").GetString());
        Assert.Equal(.25m, parameters[0].GetProperty("default").GetDecimal());
        Assert.Equal(.8m, parameters[0].GetProperty("value").GetDecimal());
        Assert.Equal(0m, parameters[0].GetProperty("minimum").GetDecimal());
        Assert.Equal(1m, parameters[0].GetProperty("maximum").GetDecimal());
        Assert.Equal("xpos", parameters[1].GetProperty("name").GetString());
        Assert.Equal(900m, parameters[1].GetProperty("value").GetDecimal());
    }

    [Fact]
    public void RuntimeFlagUsesDeclaredDefaultsAndRunAndWaveOverridesMatchProduction()
    {
        var input = Write("monitor.ss", SourceText);
        var defaultMidi = Path.Combine(root, "defaults.mid");
        var defaultRun = Run("run", input, "--runtime", "--out", defaultMidi);
        Assert.True(defaultRun.Code == 0, defaultRun.Out + defaultRun.Error);
        var defaultLiteral = SourceText[SourceText.IndexOf("perform", StringComparison.Ordinal)..];
        defaultLiteral = defaultLiteral.Replace("intensity", "0.25", StringComparison.Ordinal)
            .Replace("xpos", "200", StringComparison.Ordinal);
        Assert.Equal(SoundScriptEngine.Compile(defaultLiteral).RenderMidi(), File.ReadAllBytes(defaultMidi));

        const string literal = """
            perform expressive
            tempo 120
            track cue { gain 0.8 C4 q E4 q G4 h }
            visual "indicator" for 4s { shape circle set x 900 set opacity 0.8 }
            """;
        var expectedMidi = SoundScriptEngine.Compile(literal).RenderMidi();
        var expectedWave = SoundScriptEngine.Compile(literal).RenderWave();
        var midiPath = Path.Combine(root, "critical.mid");
        var wavePath = Path.Combine(root, "critical.wav");

        var midi = Run("run", input, "--out", midiPath, "--param=intensity=0.8", "--param", "xpos=900");
        var wave = Run("wave", input, wavePath, "--param", "intensity=0.8", "--param=xpos=900");

        Assert.True(midi.Code == 0, midi.Out + midi.Error);
        Assert.True(wave.Code == 0, wave.Out + wave.Error);
        Assert.Equal(expectedMidi, File.ReadAllBytes(midiPath));
        Assert.Equal(expectedWave, File.ReadAllBytes(wavePath));
    }

    [Theory]
    [InlineData("--param", "unknown=0.5")]
    [InlineData("--param", "intensity=1.01")]
    [InlineData("--param", "intensity=not-a-number")]
    public void InvalidRuntimeOverridesReturnUsageCodeAndPreserveExistingOutput(string option, string assignment)
    {
        var input = Write("monitor.ss", SourceText);
        var output = Write("existing.mid", "keep existing output");
        var result = Run("run", input, "--out", output, option, assignment);

        Assert.Equal(2, result.Code);
        Assert.Contains("SS0002", result.Error);
        Assert.Equal("keep existing output", File.ReadAllText(output));
    }

    [Fact]
    public void DuplicateRuntimeAssignmentsReturnUsageCode()
    {
        var input = Write("monitor.ss", SourceText);
        var result = Run("run", input, "--param", "intensity=0.5", "--param=intensity=0.8");

        Assert.Equal(2, result.Code);
        Assert.Contains("SS0002", result.Error);
        Assert.Contains("Duplicate --param", result.Error);
    }

    [Fact]
    public void ParameterDiscoveryIsOptInAndOrdinaryInspectionStillWorks()
    {
        var input = Write("plain.ss", "tempo 120 track cue { C4 q }");
        var result = Run("inspect", input, "--json");

        Assert.True(result.Code == 0, result.Out + result.Error);
        using var json = JsonDocument.Parse(result.Out);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("results").ValueKind);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, text);
        return path;
    }

    private (int Code, string Out, string Error) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(TestBuildPaths.ProjectOutput("SoundScript.Cli", "soundscript.dll"));
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment.Remove("WORDBANK_DIR");
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000))
        {
            process.Kill(true);
            throw new TimeoutException("Runtime CLI child exceeded one minute.");
        }
        return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
