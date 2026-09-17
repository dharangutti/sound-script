using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SoundScript;

namespace DevOpsSonification;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine("Usage: dotnet run --project samples/DevOpsSonification -- --total 100 --passed 96 --failed 4 [--output directory]");
            return 0;
        }

        if (!TryReadOptions(args, out var total, out var passed, out var failed, out var outputDirectory, out var error))
            return Fail(error!);

        var source = BuildSource(total, passed, failed);
        var compilation = SoundScriptEngine.Compile(source);
        var wav = compilation.RenderWave();
        var midi = compilation.RenderMidi();
        Directory.CreateDirectory(outputDirectory!);
        File.WriteAllText(Path.Combine(outputDirectory!, "devops-status.ss"), source);
        File.WriteAllBytes(Path.Combine(outputDirectory!, "devops-status.wav"), wav);
        File.WriteAllBytes(Path.Combine(outputDirectory!, "devops-status.mid"), midi);
        File.WriteAllText(Path.Combine(outputDirectory!, "manifest.txt"),
            $"total={total}\npassed={passed}\nfailed={failed}\nstatus={(failed == 0 ? "passed" : "failed")}\nwave-sha256={Hash(wav)}\nmidi-sha256={Hash(midi)}\n");

        Console.WriteLine($"Sonified {passed} passed / {failed} failed of {total} tests to {Path.GetFullPath(outputDirectory!)}");
        return 0;
    }

    private static string BuildSource(int total, int passed, int failed)
    {
        var severity = failed == 0 ? "mf" : failed * 2 >= total ? "ff" : "f";
        var passCount = Math.Min(passed, 8);
        var failCount = Math.Clamp(Math.Min(failed, 8), 0, 8);
        var passedNotes = passCount == 0 ? "rest q" : string.Join(' ', Enumerable.Repeat("G4 e", passCount));
        var failedNotes = failed == 0 ? "rest q" : string.Join(' ', Enumerable.Repeat("C3 e", failCount));
        return $"tempo 120\ntrack passed {{\n    mf\n    {passedNotes}\n}}\ntrack failed {{\n    {severity}\n    {failedNotes}\n}}\n";
    }

    private static bool TryReadOptions(string[] args, out int total, out int passed, out int failed, out string? outputDirectory, out string? error)
    {
        total = passed = failed = 0;
        outputDirectory = Path.Combine("artifacts", "samples", "devops-sonification");
        error = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                error = $"Expected an option and value, got '{args[i]}'.";
                return false;
            }
            values[args[i]] = args[++i];
        }

        if (!TryNonNegative(values, "--total", out total, out error) ||
            !TryNonNegative(values, "--passed", out passed, out error) ||
            !TryNonNegative(values, "--failed", out failed, out error))
            return false;
        if (values.TryGetValue("--output", out var requestedOutput))
            outputDirectory = requestedOutput;
        if (values.Keys.Any(key => key is not ("--total" or "--passed" or "--failed" or "--output")))
        {
            error = "Supported options are --total, --passed, --failed, and --output.";
            return false;
        }
        if (total == 0 || passed + failed > total)
        {
            error = "Provide a positive --total with --passed + --failed no greater than --total.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            error = "Output directory cannot be empty.";
            return false;
        }
        return true;
    }

    private static bool TryNonNegative(IReadOnlyDictionary<string, string> values, string key, out int value, out string? error)
    {
        error = null;
        if (!values.TryGetValue(key, out var text))
        {
            value = 0;
            error = $"Missing required option {key}.";
            return false;
        }
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0)
        {
            error = $"{key} must be a non-negative integer.";
            return false;
        }
        return true;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}
