using System.Diagnostics;
using System.Globalization;

namespace SoundScript.Media;

/// <summary>
/// A codec adapter around an existing FFmpeg executable. FFmpeg is deliberately
/// isolated here: source semantics stop at the sampled plan and audio WAV.
/// </summary>
public static class FfmpegWebmExporter
{
    public static void EnsureAvailable(string ffmpegPath)
    {
        try { Run(ffmpegPath, "-hide_banner", "-version"); }
        catch (ExportException ex) { throw new DependencyException($"FFmpeg cannot run: {ex.Message}", ex); }
    }

    public static void EnsureCapabilities(string ffmpegPath)
    {
        EnsureAvailable(ffmpegPath);
        try
        {
            var encoders = Run(ffmpegPath, "-hide_banner", "-encoders");
            foreach (var encoder in new[] { "libvpx-vp9", "libopus" })
                if (!System.Text.RegularExpressions.Regex.IsMatch(encoders, @"(?m)^\s*[VA][A-Z.]{5}\s+" + encoder + @"\s"))
                    throw new DependencyException($"FFmpeg is missing the required {encoder} encoder. Install a build with libvpx-vp9 and libopus.");
            var formats = Run(ffmpegPath, "-hide_banner", "-muxers");
            if (!System.Text.RegularExpressions.Regex.IsMatch(formats, @"(?m)^\s*E\s+webm\s"))
                throw new DependencyException("FFmpeg is missing the required WebM muxer.");
        }
        catch (ExportException ex) { throw new DependencyException($"Could not query FFmpeg capabilities: {ex.Message}", ex); }
    }

    public static void EncodeAndVerify(
        string ffmpegPath,
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVideoExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EncodeAndVerify(
            ffmpegPath,
            framesDirectory,
            audioWavPath,
            outputWebmPath,
            plan.FramesPerSecond,
            plan.DurationSeconds,
            plan.Samples.Count);
    }

    /// <summary>Encodes a canonical visual-scene plan through the same codec boundary.</summary>
    public static void EncodeAndVerify(
        string ffmpegPath,
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVisualExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        EncodeAndVerify(
            ffmpegPath,
            framesDirectory,
            audioWavPath,
            outputWebmPath,
            plan.FramesPerSecond,
            plan.DurationSeconds,
            plan.Samples.Count);
    }

    private static void EncodeAndVerify(
        string ffmpegPath,
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        int framesPerSecond,
        double durationSeconds,
        int sampleCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(framesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioWavPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputWebmPath);

        if (sampleCount == 0)
            throw new InvalidOperationException("A video export requires at least one temporal sample.");
        if (!File.Exists(audioWavPath))
            throw new FileNotFoundException("The rendered audio WAV is missing.", audioWavPath);

        AtomicOutput.Write(outputWebmPath,
            temporary => Run(ffmpegPath, BuildEncodeArguments(framesDirectory, audioWavPath, temporary, framesPerSecond, durationSeconds).ToArray()),
            temporary => Run(ffmpegPath,
                "-hide_banner", "-v", "error", "-xerror", "-nostdin",
                "-i", temporary,
                "-map", "0:v:0", "-map", "0:a:0",
                "-f", "null", "-"));
    }

    public static IReadOnlyList<string> BuildEncodeArguments(
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVideoExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return BuildEncodeArguments(framesDirectory, audioWavPath, outputWebmPath, plan.FramesPerSecond, plan.DurationSeconds);
    }

    /// <summary>Builds codec arguments from the canonical visual-scene export plan.</summary>
    public static IReadOnlyList<string> BuildEncodeArguments(
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        TemporalVisualExportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return BuildEncodeArguments(framesDirectory, audioWavPath, outputWebmPath, plan.FramesPerSecond, plan.DurationSeconds);
    }

    private static IReadOnlyList<string> BuildEncodeArguments(
        string framesDirectory,
        string audioWavPath,
        string outputWebmPath,
        int framesPerSecond,
        double durationSeconds)
    {
        return [
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-framerate", framesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-start_number", "0",
            "-i", Path.Combine(framesDirectory, "frame-%06d.ppm"),
            "-i", audioWavPath,
            "-map", "0:v:0", "-map", "1:a:0",
            // Match the browser export's quality envelope.  A 1 Mbps target
            // is visibly soft at the canonical 1280x720 scene size, especially
            // around bitmap text and high-contrast card edges.
            "-c:v", "libvpx-vp9", "-pix_fmt", "yuv420p", "-b:v", "4M",
            "-c:a", "libopus", "-b:a", "96k",
            "-t", durationSeconds.ToString("0.#########", CultureInfo.InvariantCulture),
            outputWebmPath,
        ];
    }

    private static string Run(string executable, params string[] arguments)
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
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEndAsync();
            // Both redirected pipes must drain concurrently, including capability listings.
            if (!process.WaitForExit(600_000))
            {
                process.Kill(entireProcessTree: true);
                throw new ExportException("FFmpeg timed out after ten minutes.");
            }
            Task.WaitAll(stderr, stdout);
            if (process.ExitCode != 0)
                throw new ExportException($"FFmpeg exited with code {process.ExitCode}:\n{stderr.Result}{stdout.Result}");
            return stdout.Result + stderr.Result;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new DependencyException(
                "FFmpeg is required for CLI WebM encoding. Install an FFmpeg build with libvpx-vp9 and libopus, " +
                "put it on PATH, or pass --ffmpeg <path-to-ffmpeg>.", ex);
        }
    }
}
