using System.Diagnostics;
using System.Globalization;

namespace SoundScript.Media;

/// <summary>
/// A codec adapter around an existing FFmpeg executable. FFmpeg is deliberately
/// isolated here: source semantics stop at the sampled plan and audio WAV.
/// </summary>
public static class FfmpegWebmExporter
{
    public static void EnsureAvailable(string ffmpegPath) => Run(ffmpegPath, "-hide_banner", "-version");

    public static void EncodeAndVerify(
        string ffmpegPath,
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVideoExportPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(framesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioWavPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputWebmPath);
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Samples.Count == 0)
            throw new InvalidOperationException("A video export requires at least one temporal sample.");
        if (!File.Exists(audioWavPath))
            throw new FileNotFoundException("The rendered audio WAV is missing.", audioWavPath);

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputWebmPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        Run(ffmpegPath, BuildEncodeArguments(framesDirectory, audioWavPath, outputWebmPath, plan).ToArray());

        if (!File.Exists(outputWebmPath) || new FileInfo(outputWebmPath).Length == 0)
            throw new InvalidOperationException("FFmpeg completed without creating a WebM file.");

        // Decode both explicitly mapped streams. This validates the final file
        // contains a readable video stream and a readable audio stream.
        Run(ffmpegPath,
            "-hide_banner", "-v", "error",
            "-i", outputWebmPath,
            "-map", "0:v:0", "-map", "0:a:0",
            "-f", "null", "-");
    }

    public static IReadOnlyList<string> BuildEncodeArguments(
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVideoExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [
            "-hide_banner", "-loglevel", "error", "-y",
            "-framerate", plan.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-start_number", "0",
            "-i", Path.Combine(framesDirectory, "frame-%06d.ppm"),
            "-i", audioWavPath,
            "-map", "0:v:0", "-map", "1:a:0",
            "-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-b:v", "1M",
            "-c:a", "libopus", "-b:a", "96k",
            "-t", plan.DurationSeconds.ToString("0.#########", CultureInfo.InvariantCulture),
            outputWebmPath,
        ];
    }

    private static void Run(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start FFmpeg at '{executable}'.");
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}:\n{stderr}{stdout}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "FFmpeg is required for CLI WebM encoding. Install an FFmpeg build with libvpx-vp9 and libopus, " +
                "put it on PATH, or pass --ffmpeg <path-to-ffmpeg>.", ex);
        }
    }
}
