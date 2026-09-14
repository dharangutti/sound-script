using SoundScript.Media;
using SoundScript.Wave.Io;

namespace SoundScript.Cli;

public sealed record VideoPreflightResult(string Ffmpeg, int Width, int Height, int Fps, int EstimatedFrameCount, double DurationSeconds, string? Output, string AudioFitPolicy);

public static class VideoPreflight
{
    public static VideoPreflightResult Check(CliArguments args, SourceAnalysis analysis)
    {
        var duration = analysis.Timeline!.Duration.TotalSeconds;
        if (duration <= 0) throw new InvalidOperationException("Video export requires a positive visual timeline duration.");
        var width = args.Integer("width", 1280); var height = args.Integer("height", 720); var fps = args.Integer("fps", 30);
        if (width > 3840 || height > 2160) throw new CliUsageException("Video frame dimensions must be even and within 2–3840 × 2–2160.");
        int frames;
        try { frames = checked((int)Math.Ceiling(duration * fps)); _ = checked((int)Math.Ceiling(duration * WavWriter.SampleRate)); }
        catch (OverflowException) { throw new InvalidOperationException("Video duration exceeds the current frame/audio buffer capacity."); }
        if (args.Value("out") is { } output) AtomicOutput.ValidatePath(output);
        var ffmpeg = args.Value("ffmpeg") ?? Environment.GetEnvironmentVariable("SOUNDSCRIPT_FFMPEG") ?? "ffmpeg";
        FfmpegWebmExporter.EnsureCapabilities(ffmpeg);
        return new(ffmpeg, width, height, fps, frames, duration, args.Value("out"), "Pad or truncate audio to visual duration (existing media policy).");
    }
}
