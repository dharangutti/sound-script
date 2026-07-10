using SoundScript.Vocal;
using SoundScript.Wave.Io;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public class WordbankVocalEngineTests
{
    public WordbankVocalEngineTests()
    {
        WordbankCatalog.ResetToEmbedded();
        CorpusCatalog.Reset();
        CorpusCatalog.TryLoadEmbedded();
    }

    [Fact]
    public void CorpusCatalog_LoadsEmbeddedPilotEntries()
    {
        Assert.True(CorpusCatalog.IsLoaded);
        Assert.True(CorpusCatalog.TryGetLemma("en", "hello", out var entry));
        Assert.Equal("audio/en/hello.wav", entry.Audio);
        Assert.NotNull(CorpusCatalog.ResolveAudioPath(entry));
    }

    [Fact]
    public void WordbankEngine_UsesCorpusAudioForKnownWords()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("phrase.wav");
        new WordbankVocalEngine().Synthesize("Hello welcome", outPath, new VocalEngineOptions { Locale = "en" });

        Assert.True(File.Exists(outPath));
        Assert.True(PeakPcm16(WavReader.ReadMono(outPath)) >= 20_000);
    }

    [Fact]
    public void WordbankEngine_G2PFallbackForUnknownWord()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("oov.wav");
        new WordbankVocalEngine().Synthesize("xenoglottophobia", outPath, new VocalEngineOptions { Locale = "en" });

        Assert.True(File.Exists(outPath));
        Assert.True(PeakPcm16(WavReader.ReadMono(outPath)) >= 15_000);
    }

    [Fact]
    public void CompositeEngine_MixesCorpusAndFallback()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("mixed.wav");
        new CompositeVocalEngine().Synthesize("Hello xenoglottophobia", outPath, new VocalEngineOptions { Locale = "en" });

        Assert.True(File.Exists(outPath));
        Assert.True(PeakPcm16(WavReader.ReadMono(outPath)) >= 15_000);
    }

    [Fact]
    public void CompositeEngine_PhraseWithCorpusAndOovWords_IsAudibleThroughout()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("phrase.wav");
        new CompositeVocalEngine().Synthesize(
            "Hello welcome to SoundScript",
            outPath,
            new VocalEngineOptions { Locale = "en" });

        var samples = WavReader.ReadMono(outPath);
        Assert.True(PeakPcm16(samples) >= 20_000);

        // Later words must not be buried: tail quarter should stay audible.
        var tailStart = (int)(samples.Length * 0.55);
        var tail = samples.AsSpan(tailStart);
        var tailPeak = 0.0;
        foreach (var sample in tail)
            tailPeak = Math.Max(tailPeak, Math.Abs(sample));

        Assert.True(tailPeak >= 0.08, "Tail of phrase should remain audible after corpus words.");
    }

    [Fact]
    public void VocalStemProcessor_TrimsAndAppliesGain()
    {
        var input = Enumerable.Range(0, 4410).Select(i => (float)Math.Sin(i * 0.1)).ToArray();
        var processed = VocalStemProcessor.ApplyTransform(input, trimStartMs: 50, trimEndMs: 80, gain: 0.5, pitchSemitones: 0);

        Assert.True(processed.Length > 0);
        Assert.True(processed.Length < input.Length);
    }

    [Fact]
    public void Cli_VocalGenerate_WordbankEngine_WithEmbeddedCorpus()
    {
        using var dir = new TempOutputDirectory();
        var outPath = dir.FilePath("hello.wav");

        var (exitCode, stdout, _) = RunCli(
            $"vocal generate \"Hello welcome\" --out \"{outPath}\" --engine wordbank --locale en");

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outPath));
        Assert.Contains("wordbank", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.True(PeakPcm16(WavReader.ReadMono(outPath)) >= 20_000);
    }

    private static int PeakPcm16(float[] samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
            peak = Math.Max(peak, Math.Abs(sample));
        return (int)Math.Round(peak * short.MaxValue);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string arguments)
    {
        var cliDll = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../SoundScript.Cli/bin/Debug/net8.0/soundscript.dll"));

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{cliDll}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private sealed class TempOutputDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-wb-vocal-" + Guid.NewGuid().ToString("N"));

        public TempOutputDirectory() => Directory.CreateDirectory(Root);

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
