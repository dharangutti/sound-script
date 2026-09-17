using System.Security.Cryptography;
using System.Text;
using SoundScript;

namespace DynamicAudio;

internal static class Program
{
    private sealed record ApplicationEvent(string Name, int Tempo, string Dynamic, string Motif);

    private static readonly ApplicationEvent[] Events =
    [
        new("welcome", 88, "mp", "C4 e E4 e G4 q"),
        new("success", 120, "mf", "C4 e E4 e G4 e C5 q"),
        new("warning", 104, "f", "A3 e A3 e E4 q A3 e"),
        new("new-message", 112, "mf", "G4 e B4 e D5 q"),
        new("order-complete", 128, "f", "C4 e G4 e C5 e E5 q"),
        new("deployment-failed", 72, "ff", "C3 e C3 e G2 q C3 e")
    ];

    public static int Main(string[] args)
    {
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine("Usage: dotnet run --project samples/DynamicAudio -- [output-directory]");
            return 0;
        }

        if (args.Length > 1)
            return Fail("Expected zero or one output directory argument.");

        var outputDirectory = args.Length == 1 ? args[0] : Path.Combine("artifacts", "samples", "dynamic-audio");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return Fail("Output directory cannot be empty.");

        Directory.CreateDirectory(outputDirectory);
        var manifest = new StringBuilder()
            .AppendLine("Dynamic application audio fixtures")
            .AppendLine("Each file is selected from runtime application state.");

        foreach (var applicationEvent in Events)
        {
            var source = $"tempo {applicationEvent.Tempo}\ntrack cue {{\n    {applicationEvent.Dynamic}\n    {applicationEvent.Motif}\n}}\n";
            var wav = SoundScriptEngine.Compile(source).RenderWave();
            var path = Path.Combine(outputDirectory, applicationEvent.Name + ".wav");
            File.WriteAllBytes(path, wav);
            manifest.AppendLine($"{applicationEvent.Name}: tempo={applicationEvent.Tempo}, dynamic={applicationEvent.Dynamic}, sha256={Hash(wav)}");
        }

        File.WriteAllText(Path.Combine(outputDirectory, "manifest.txt"), manifest.ToString());
        Console.WriteLine($"Rendered {Events.Length} application events to {Path.GetFullPath(outputDirectory)}");
        return 0;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}
