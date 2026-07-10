using System.Diagnostics;

namespace SoundScript.Vocal.Wordbank;

/// <summary>
/// Produces a raw single-word WAV for the auto-generator. Abstracted so tests can
/// substitute a deterministic fake without requiring eSpeak on the host.
/// </summary>
public interface IEspeakRawSynthesizer
{
    /// <summary>True when the underlying generator can run on this host.</summary>
    bool IsAvailable { get; }

    /// <summary>Version string recorded as <c>generatorVersion</c> in lemma metadata.</summary>
    string GeneratorVersion { get; }

    /// <summary>Renders <paramref name="text"/> to a WAV at <paramref name="outputWavPath"/>.</summary>
    void Synthesize(string text, string voice, string outputWavPath);
}

/// <summary>
/// Default <see cref="IEspeakRawSynthesizer"/> backed by <c>espeak-ng</c>/<c>espeak</c>.
/// Reuses the repo's existing eSpeak invocation (<see cref="EspeakNgVocalEngine"/>,
/// fixed <c>-v/-w</c> flags) so generated audio stays consistent with the offline
/// vocal engine, then leaves normalization to <see cref="WordbankNormalizer"/>.
/// </summary>
public sealed class EspeakRawSynthesizer : IEspeakRawSynthesizer
{
    private readonly Lazy<string> _version = new(ResolveVersion);

    public bool IsAvailable => EspeakNgVocalEngine.ResolveExecutable() is not null;

    public string GeneratorVersion => _version.Value;

    public void Synthesize(string text, string voice, string outputWavPath)
    {
        var options = new VocalEngineOptions { Voice = voice };
        new EspeakNgVocalEngine().Synthesize(text, outputWavPath, options);
    }

    private static string ResolveVersion()
    {
        var executable = EspeakNgVocalEngine.ResolveExecutable();
        if (executable is null)
            return "unknown";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return "unknown";

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Example: "eSpeak NG text-to-speech: 1.51  Data at: ..."
            return ParseVersion(stdout);
        }
        catch
        {
            return "unknown";
        }
    }

    internal static string ParseVersion(string versionOutput)
    {
        if (string.IsNullOrWhiteSpace(versionOutput))
            return "unknown";

        var firstLine = versionOutput.Split('\n', '\r').FirstOrDefault(l => l.Length > 0) ?? "";
        var colon = firstLine.IndexOf(':');
        if (colon >= 0 && colon + 1 < firstLine.Length)
        {
            var tail = firstLine[(colon + 1)..].Trim();
            var token = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return firstLine.Trim();
    }
}
