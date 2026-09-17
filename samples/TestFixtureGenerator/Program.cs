using System.Security.Cryptography;
using System.Text.Json;
using SoundScript;

namespace TestFixtureGenerator;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine("Usage: dotnet run --project samples/TestFixtureGenerator -- [output-directory] [seed]");
            return 0;
        }
        if (args.Length > 2)
            return Fail("Expected zero, one, or two arguments: output directory and optional integer seed.");
        var outputDirectory = args.Length > 0 ? args[0] : Path.Combine("artifacts", "samples", "test-fixture-generator");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return Fail("Output directory cannot be empty.");
        if (args.Length > 1 && (!int.TryParse(args[1], out var seed) || seed < 0))
            return Fail("Seed must be a non-negative integer.");
        var selectedSeed = args.Length > 1 ? int.Parse(args[1]) : 42;

        var source = BuildSource(selectedSeed);
        var compilation = SoundScriptEngine.Compile(source);
        var waveA = compilation.RenderWave();
        var waveB = compilation.RenderWave();
        var midiA = compilation.RenderMidi();
        var midiB = compilation.RenderMidi();
        if (!waveA.AsSpan().SequenceEqual(waveB) || !midiA.AsSpan().SequenceEqual(midiB))
            return Fail("Determinism check failed: repeated renders differ.");

        Directory.CreateDirectory(outputDirectory);
        var sourcePath = Path.Combine(outputDirectory, "fixture.ss");
        var wavePath = Path.Combine(outputDirectory, "fixture.wav");
        var midiPath = Path.Combine(outputDirectory, "fixture.mid");
        File.WriteAllText(sourcePath, source);
        File.WriteAllBytes(wavePath, waveA);
        File.WriteAllBytes(midiPath, midiA);

        // A consumer-style check reloads the checked-in source through the facade and compares exact bytes.
        var consumer = SoundScriptEngine.CompileFile(sourcePath);
        var consumerWave = consumer.RenderWave();
        var consumerMidi = consumer.RenderMidi();
        if (!waveA.AsSpan().SequenceEqual(consumerWave) || !midiA.AsSpan().SequenceEqual(consumerMidi))
            return Fail("Consumer check failed: reloaded fixture differs from the generated fixture.");

        var manifest = new FixtureManifest(selectedSeed, Hash(waveA), Hash(midiA), waveA.Length, midiA.Length, "passed");
        File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        File.WriteAllText(Path.Combine(outputDirectory, "consumer-check.txt"), "PASS: same input produced byte-identical WAV and MIDI; CompileFile consumer check passed." + Environment.NewLine);
        Console.WriteLine($"Generated deterministic WAV/MIDI fixtures in {Path.GetFullPath(outputDirectory)} (seed {selectedSeed})");
        return 0;
    }

    private static string BuildSource(int seed)
    {
        var notes = new[] { "C4", "E4", "G4", "B4", "D5", "G4" };
        var offset = seed % notes.Length;
        var motif = string.Join(' ', Enumerable.Range(0, 6).Select(index => $"{notes[(index + offset) % notes.Length]} e"));
        return $"// Generated fixture; seed={seed}\ntempo 120\ntrack fixture {{\n    instrument piano\n    {motif}\n}}\n";
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    private sealed record FixtureManifest(int Seed, string WaveSha256, string MidiSha256, int WaveBytes, int MidiBytes, string ConsumerCheck);
}
