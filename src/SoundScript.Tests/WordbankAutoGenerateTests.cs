using System.Text.Json;
using SoundScript.Vocal.Wordbank;
using SoundScript.Wave.Io;
using SoundScript.Wordbank;
using Xunit;

namespace SoundScript.Tests;

[Collection("WordbankCatalog")]
public class WordbankAutoGenerateTests : IDisposable
{
    private readonly string _root;

    public WordbankAutoGenerateTests()
    {
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
        CorpusCatalog.Reset();

        _root = Path.Combine(Path.GetTempPath(), "ss-wb-autogen-" + Guid.NewGuid().ToString("N"));
        CreateMinimalCorpus(_root);
        Assert.True(CorpusCatalog.TryLoadFromWordbankRoot(_root, out var error), error);
    }

    public void Dispose()
    {
        CorpusCatalog.Reset();
        WordbankCatalog.ResetToEmbedded();
        WordbankCatalog.ResetActive();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void EnsureLemma_MissingWithFlag_GeneratesNormalizesAndResolves()
    {
        var generator = new WordbankAutoGenerate(espeak: new FakeEspeak());

        var result = generator.EnsureLemma("newword", "en", autoGenerateMissing: true);

        Assert.Equal(WordbankAutoGenerateStatus.Generated, result.Status);
        Assert.True(result.Resolved);
        Assert.NotNull(result.Path);
        Assert.True(File.Exists(result.Path));
        Assert.EndsWith(Path.Combine("audio", "en", "normalized", "newword.wav"), result.Path);

        // Subsequent lookup resolves from the normalized path.
        Assert.True(CorpusCatalog.TryGetLemma("en", "newword", out var entry));
        Assert.Equal("audio/en/normalized/newword.wav", entry.Audio);
        Assert.Equal(result.Path, CorpusCatalog.ResolveAudioPath(entry));

        // Generator provenance persisted to lemmas.json.
        var stored = LoadEntry("newword");
        Assert.Equal("espeak-ng", stored.GetProperty("generator").GetString());
        Assert.Equal("test-9.9", stored.GetProperty("generatorVersion").GetString());
        Assert.Equal(WordbankNormalizer.NormalizerVersion, stored.GetProperty("normalizerVersion").GetInt32());
        Assert.True(stored.TryGetProperty("generatedAt", out var generatedAt));
        Assert.False(string.IsNullOrWhiteSpace(generatedAt.GetString()));
    }

    [Fact]
    public void EnsureLemma_RepeatedRuns_AreIdempotent()
    {
        var generator = new WordbankAutoGenerate(espeak: new FakeEspeak());

        var first = generator.EnsureLemma("idem", "en", autoGenerateMissing: true);
        Assert.Equal(WordbankAutoGenerateStatus.Generated, first.Status);

        var lemmaFile = Path.Combine(_root, "corpus", "v2026.07", "en", "lemmas.json");
        var wavBefore = File.ReadAllBytes(first.Path!);
        var sidecarBefore = File.ReadAllBytes(Path.ChangeExtension(first.Path!, ".json"));
        var lemmasBefore = File.ReadAllBytes(lemmaFile);

        var second = generator.EnsureLemma("idem", "en", autoGenerateMissing: true);

        Assert.Equal(WordbankAutoGenerateStatus.AlreadyPresent, second.Status);
        Assert.True(second.Resolved);
        Assert.Equal(first.Path, second.Path);

        Assert.Equal(wavBefore, File.ReadAllBytes(first.Path!));
        Assert.Equal(sidecarBefore, File.ReadAllBytes(Path.ChangeExtension(first.Path!, ".json")));
        Assert.Equal(lemmasBefore, File.ReadAllBytes(lemmaFile));
    }

    [Fact]
    public void EnsureLemma_MissingWithoutFlag_DoesNotGenerate()
    {
        var generator = new WordbankAutoGenerate(espeak: new FakeEspeak());

        var result = generator.EnsureLemma("disabled", "en", autoGenerateMissing: false);

        Assert.Equal(WordbankAutoGenerateStatus.MissingGenerationDisabled, result.Status);
        Assert.False(result.Resolved);
        Assert.Null(result.Path);
        Assert.False(File.Exists(Path.Combine(_root, "corpus", "v2026.07", "audio", "en", "normalized", "disabled.wav")));
    }

    [Fact]
    public void EnsureLemma_GeneratorUnavailable_ReportsClearly()
    {
        var generator = new WordbankAutoGenerate(espeak: new FakeEspeak { Available = false });

        var result = generator.EnsureLemma("noespeak", "en", autoGenerateMissing: true);

        Assert.Equal(WordbankAutoGenerateStatus.GeneratorUnavailable, result.Status);
        Assert.False(result.Resolved);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void EnsureLemma_WithRealEspeak_GeneratesWhenInstalled()
    {
        if (!new EspeakRawSynthesizer().IsAvailable)
            return; // eSpeak not installed on this host — integration path covered by fake-backed tests.

        var generator = new WordbankAutoGenerate();
        var result = generator.EnsureLemma("hazelnut", "en", autoGenerateMissing: true);

        Assert.Equal(WordbankAutoGenerateStatus.Generated, result.Status);
        Assert.True(File.Exists(result.Path));
        Assert.True(AudiblePeak(WavReader.ReadMono(result.Path!)) > 0.05);

        // Re-running resolves without regenerating.
        var again = generator.EnsureLemma("hazelnut", "en", autoGenerateMissing: true);
        Assert.Equal(WordbankAutoGenerateStatus.AlreadyPresent, again.Status);
    }

    [Fact]
    public void Cli_WordbankEnsure_AutoGenerateMissing_ThenResolves()
    {
        if (!new EspeakRawSynthesizer().IsAvailable)
            return; // eSpeak not installed — CLI path relies on the real binary.

        var wordbankRoot = TryResolveWordbankRoot();
        if (wordbankRoot is null)
            return; // no wordbank checkout available in this environment

        var temp = Path.Combine(Path.GetTempPath(), "ss-wb-cli-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(wordbankRoot, temp);
        try
        {
            const string lemma = "quokka";
            var normalized = Path.Combine(temp, "corpus", "v2026.07", "audio", "en", "normalized", $"{lemma}.wav");

            var first = RunCli($"wordbank ensure {lemma} --locale en --wordbank-dir \"{temp}\" --auto-generate-missing");
            Assert.Equal(0, first.ExitCode);
            Assert.Contains("Generated", first.StdOut);
            Assert.True(File.Exists(normalized));

            var second = RunCli($"wordbank ensure {lemma} --locale en --wordbank-dir \"{temp}\"");
            Assert.Equal(0, second.ExitCode);
            Assert.Contains("Resolved", second.StdOut);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private static string? TryResolveWordbankRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var name in new[] { "wordbank", "../soundscript-wordbank" })
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, name));
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                    return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            if (dir.Contains($"{Path.DirectorySeparatorChar}.git"))
                continue;
            Directory.CreateDirectory(dir.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}.git"))
                continue;
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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
            UseShellExecute = false,
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }

    private JsonElement LoadEntry(string lemma)
    {
        var lemmaFile = Path.Combine(_root, "corpus", "v2026.07", "en", "lemmas.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(lemmaFile));
        foreach (var entry in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (string.Equals(entry.GetProperty("lemma").GetString(), lemma, StringComparison.OrdinalIgnoreCase))
                return entry.Clone();
        }

        throw new InvalidOperationException($"Entry '{lemma}' not found in lemmas.json.");
    }

    private static double AudiblePeak(float[] samples)
    {
        var peak = 0.0;
        foreach (var s in samples)
            peak = Math.Max(peak, Math.Abs(s));
        return peak;
    }

    private static void CreateMinimalCorpus(string root)
    {
        var corpusDir = Path.Combine(root, "corpus", "v2026.07");
        var enDir = Path.Combine(corpusDir, "en");
        Directory.CreateDirectory(enDir);

        File.WriteAllText(Path.Combine(corpusDir, "manifest.json"),
            """
            {
              "id": "2026.07",
              "version": 1,
              "status": "pilot",
              "locales": [
                { "code": "en", "path": "en", "lemmaFile": "lemmas.json" }
              ],
              "engineCompatibility": { "soundscriptMin": "8.0.0", "wordbankMin": "0.6.0" }
            }
            """);

        File.WriteAllText(Path.Combine(enDir, "lemmas.json"),
            """
            {
              "corpusId": "2026.07",
              "locale": "en",
              "version": 0,
              "status": "pilot",
              "entries": []
            }
            """);
    }

    /// <summary>Deterministic stand-in for eSpeak so tests never depend on the host binary.</summary>
    private sealed class FakeEspeak : IEspeakRawSynthesizer
    {
        public bool Available { get; init; } = true;

        public bool IsAvailable => Available;

        public string GeneratorVersion => "test-9.9";

        public void Synthesize(string text, string voice, string outputWavPath)
        {
            const int sampleRate = WavWriter.SampleRate;
            var samples = new float[sampleRate / 2];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = (float)(0.6 * Math.Sin(2 * Math.PI * 180 * i / sampleRate));

            var dir = Path.GetDirectoryName(Path.GetFullPath(outputWavPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            WavWriter.Write(outputWavPath, samples);
        }
    }
}
