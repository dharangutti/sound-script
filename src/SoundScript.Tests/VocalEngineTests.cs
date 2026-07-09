using System.Diagnostics;
using SoundScript.Vocal;
using SoundScript.Wave.Io;
using SoundScript.Wave.Tts;
using Xunit;

namespace SoundScript.Tests;

public class VocalEngineTests
{
    [Fact]
    public void ProsodyEngine_GeneratesNonEmptyWav()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("hello.wav");
        var engine = new ProsodyVocalEngine();

        engine.Synthesize("hello world", outPath, new VocalEngineOptions { Seed = 7 });

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 44);
        var samples = WavReader.ReadMono(outPath);
        Assert.NotEmpty(samples);
        Assert.True(PeakPcm16(samples) >= 20_000, "Vocal stem should be loud enough to hear clearly.");
    }

    [Fact]
    public void ProsodyEngine_NormalizesQuietOutputToAudiblePeak()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("boosted.wav");
        new ProsodyVocalEngine().Synthesize("test", outPath, new VocalEngineOptions { Seed = 3 });
        Assert.InRange(PeakPcm16(WavReader.ReadMono(outPath)), 25_000, 32_767);
    }

    [Fact]
    public void Example_JingleBellsVocal_OfflineTts_RendersAudibleVocal()
    {
        var exampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples"));
        var script = Path.Combine(exampleDir, "jingle-bells-vocal.ssw");
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("jingle.wav");
        var stemsDir = dir.FilePath("vocal-stems");

        Assert.Equal(0, RunCli(
            $"wave \"{script}\" \"{outPath}\" --offline-tts prosody --offline-tts-dir \"{stemsDir}\"").ExitCode);

        Assert.True(File.Exists(outPath));
        Assert.True(File.Exists(Path.Combine(stemsDir, "jingle-bells-jingle-bells.wav")));
        Assert.True(File.Exists(Path.Combine(stemsDir, "jingle-all-the-way.wav")));
        Assert.True(PeakPcm16(WavReader.ReadMono(outPath)) >= 15_000);
        Assert.True(PeakPcm16(WavReader.ReadMono(Path.Combine(stemsDir, "jingle-bells-jingle-bells.wav"))) >= 20_000);
    }

    private static int PeakPcm16(float[] samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));

        return (int)Math.Round(peak * short.MaxValue);
    }

    [Fact]
    public void ProsodyEngine_IsDeterministicForSameSeed()
    {
        using var dir = new TempOutputDirectory();
        var a = dir.FilePath("a.wav");
        var b = dir.FilePath("b.wav");
        var engine = new ProsodyVocalEngine();
        var options = new VocalEngineOptions { Seed = 42 };

        engine.Synthesize("deterministic phrase", a, options);
        engine.Synthesize("deterministic phrase", b, options);

        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
    }

    [Fact]
    public void BatchExporter_GeneratesSlugNamedStems()
    {
        using var dir = new TempOutputDirectory();
        var script = dir.FilePath("song.ssw");
        var outDir = dir.FilePath("stems");
        File.WriteAllText(script,
            """
            tempo 120
            track pad { mf C4 w }
            speak "Hello world" seed=1
            speak "Goodbye" seed=2
            """);

        var engine = new ProsodyVocalEngine();
        var items = VocalBatchExporter.ExportFromScript(script, outDir, engine, new VocalEngineOptions { Seed = 7 });

        Assert.Equal(2, items.Count);
        Assert.True(File.Exists(Path.Combine(outDir, "hello-world.wav")));
        Assert.True(File.Exists(Path.Combine(outDir, "goodbye.wav")));
    }

    [Fact]
    public void BatchExporter_SkipsSpeakWithSamplePath()
    {
        using var dir = new TempOutputDirectory();
        var script = dir.FilePath("song.ssw");
        var outDir = dir.FilePath("stems");
        File.WriteAllText(script,
            """
            tempo 120
            speak "recorded" sample="take.wav" seed=1
            speak "synthetic" seed=2
            """);

        var engine = new ProsodyVocalEngine();
        var items = VocalBatchExporter.ExportFromScript(script, outDir, engine, new VocalEngineOptions());

        Assert.Single(items);
        Assert.Equal("synthetic", items[0].Text);
        Assert.True(File.Exists(Path.Combine(outDir, "synthetic.wav")));
    }

    [Fact]
    public void TtsDirectoryMapper_Slugify_MatchesBatchNaming()
    {
        Assert.Equal("hello-world", TtsDirectoryMapper.Slugify("Hello world"));
        Assert.Equal("speech", TtsDirectoryMapper.Slugify("!!!"));
    }

    [Fact]
    public void Factory_CreateDefault_UsesProsodyWhenEspeakMissing()
    {
        if (EspeakNgVocalEngine.ResolveExecutable() is not null)
            return;

        var engine = VocalEngineFactory.CreateDefault();
        Assert.Equal("prosody", engine.Name);
    }

    [Fact]
    public void Cli_VocalGenerate_WritesWavWithProsodyEngine()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("stem.wav");

        var (exitCode, stdout, _) = RunCli(
            $"vocal generate \"hello world\" --out \"{outPath}\" --engine prosody --seed=7");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outPath));
        Assert.Contains("prosody", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cli_VocalBatch_ExportsAllSpeakPhrases()
    {
        using var dir = new TempOutputDirectory();
        var script = dir.FilePath("song.ssw");
        var outDir = dir.FilePath("stems");
        File.WriteAllText(script,
            """
            tempo 120
            speak "batch one" seed=1
            speak "batch two" seed=2
            """);

        var (exitCode, stdout, _) = RunCli(
            $"vocal batch \"{script}\" --out-dir \"{outDir}\" --engine prosody");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "batch-one.wav")));
        Assert.True(File.Exists(Path.Combine(outDir, "batch-two.wav")));
        Assert.Contains("2 vocal stem", stdout);
    }

    [Fact]
    public void Cli_Wave_OfflineTts_GeneratesStemsAndRenders()
    {
        using var dir = new TempOutputDirectory();
        var script = dir.FilePath("song.ssw");
        var outPath = dir.FilePath("mix.wav");
        File.WriteAllText(script,
            """
            tempo 120
            track pad { mf C4 h }
            speak "offline test" seed=3
            """);

        var (exitCode, _, stderr) = RunCli(
            $"wave \"{script}\" \"{outPath}\" --offline-tts prosody --seed=3");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 44);
        Assert.Contains("offline-tts", stderr);
        Assert.True(Directory.Exists(dir.FilePath("vocal-stems")));
        Assert.True(File.Exists(dir.FilePath("vocal-stems/offline-test.wav")));
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
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-vocal-" + Guid.NewGuid().ToString("N"));

        public TempOutputDirectory() => Directory.CreateDirectory(Root);

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
