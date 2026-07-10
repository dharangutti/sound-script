using System.Diagnostics;
using SoundScript.Wave.Io;

namespace SoundScript.Vocal;

/// <summary>
/// Spawns <c>espeak-ng</c> or <c>espeak</c> when installed on the host (offline, no cloud).
/// Output is normalized to 44.1 kHz mono via <see cref="WavReader"/>.
/// </summary>
public sealed class EspeakNgVocalEngine : IVocalEngine
{
    public string Name => "espeak";

    public void Synthesize(string text, string outputWavPath, VocalEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var executable = ResolveExecutable()
            ?? throw new InvalidOperationException(
                "eSpeak is not installed. Install espeak-ng (recommended) or use --engine prosody for the built-in synthetic voice.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputWavPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(Path.GetTempPath(), $"soundscript-espeak-{Guid.NewGuid():N}.wav");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList =
                {
                    "-v", options.Voice,
                    "-w", tempPath,
                    text,
                },
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {executable}.");

            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(tempPath))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stderr)
                        ? $"{executable} failed with exit code {process.ExitCode}."
                        : stderr.Trim());
            }

            var mono = WavReader.ReadMono(tempPath);
            mono = VocalStemNormalizer.Normalize(mono, options.OutputGain);

            if (VocalStemNormalizer.Peak(mono) <= 1e-6)
            {
                throw new InvalidOperationException(
                    $"{executable} produced a silent WAV — try --engine prosody or check the voice id.");
            }

            WavWriter.Write(outputWavPath, mono);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>Renders a short phrase to mono samples without writing a file.</summary>
    internal static float[] SynthesizeToSamples(string text, VocalEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var executable = ResolveExecutable()
            ?? throw new InvalidOperationException("eSpeak is not installed.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"soundscript-espeak-{Guid.NewGuid():N}.wav");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                ArgumentList = { "-v", options.Voice, "-w", tempPath, text },
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {executable}.");

            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(tempPath))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(stderr)
                        ? $"{executable} failed with exit code {process.ExitCode}."
                        : stderr.Trim());
            }

            var mono = WavReader.ReadMono(tempPath);
            mono = VocalStemNormalizer.Normalize(mono, options.OutputGain);

            if (VocalStemNormalizer.Peak(mono) <= 1e-6)
                throw new InvalidOperationException($"{executable} produced a silent WAV.");

            return mono;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    internal static string? ResolveExecutable()
    {
        foreach (var name in new[] { "espeak-ng", "espeak" })
        {
            var path = FindOnPath(name);
            if (path is not null)
                return path;
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar))
            return null;

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(candidate))
                return candidate;

            if (OperatingSystem.IsWindows())
            {
                var withExe = candidate + ".exe";
                if (File.Exists(withExe))
                    return withExe;
            }
        }

        return null;
    }
}
