using SoundScript.Media;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SoundScript.Transcription;

/// <summary>Desktop adapter; browser callers provide decoded PCM instead of invoking a process.</summary>
public sealed class DesktopMediaInput(string? ffmpeg = null) : ITranscriptionInputAdapter<string>
{
    private string Executable => ffmpeg ?? Environment.GetEnvironmentVariable("SOUNDSCRIPT_FFMPEG") ?? "ffmpeg";
    private static void ValidateFile(string input)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("Media input was not found.",input);
        string extension=Path.GetExtension(input).ToLowerInvariant();
        if (!new[]{".wav",".mp3",".mp4",".m4a",".webm",".ogg",".flac",".aac",".mov"}.Contains(extension))
            throw new NotSupportedException($"Unsupported media extension '{extension}'. Use WAV, MP3, MP4, M4A, WebM, Ogg, FLAC, AAC or MOV.");
        if (new FileInfo(input).Length>64*1024*1024) throw new InvalidDataException("Media exceeds the 64 MiB input limit.");
    }
    public async Task<MediaInfo> InspectAsync(string input, CancellationToken cancellationToken = default)
    {
        ValidateFile(input);
        if (Path.GetExtension(input).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                MediaInfo? info = null;
                PcmWaveInput.Decode(await File.ReadAllBytesAsync(input,cancellationToken), new(0,.00001), value => info = value);
                return info!;
            }
            catch (NotSupportedException) { }
        }
        // Reuse FFmpeg itself: no additional ffprobe dependency. Decode zero output seconds.
        var log = await Task.Run(() => FfmpegWebmExporter.RunMediaProcess(Executable,
            ["-nostdin","-hide_banner","-i",Path.GetFullPath(input),"-map","0:a:0","-t","0","-f","null","-"],cancellationToken),cancellationToken);
        var match = Regex.Match(log, @"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");
        if (!match.Success) throw new InvalidDataException("Cannot determine media duration. Convert to PCM WAV before selecting an excerpt.");
        double seconds = double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture)*3600
            + double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)*60 + double.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture);
        var audio = Regex.Match(log, @"Audio: [^\r\n]*?, (\d+) Hz, (mono|stereo|\d+ channels)");
        int rate = audio.Success ? int.Parse(audio.Groups[1].Value,CultureInfo.InvariantCulture) : 0;
        int channels = audio.Success ? audio.Groups[2].Value switch { "mono" => 1, "stereo" => 2, var text => int.Parse(text.Split(' ')[0],CultureInfo.InvariantCulture) } : 0;
        return new(seconds,rate,channels);
    }
    public Task<AnalysisAudio> DecodeAsync(string input, CancellationToken cancellationToken = default)
        => DecodeAsync(input, new MediaExcerpt(), cancellationToken);
    public async Task<AnalysisAudio> DecodeAsync(string input, MediaExcerpt excerpt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateFile(input);
        if (Path.GetExtension(input).Equals(".wav",StringComparison.OrdinalIgnoreCase))
        {
            try { return PcmWaveInput.Decode(await File.ReadAllBytesAsync(input,cancellationToken),excerpt); }
            catch (NotSupportedException) { }
        }
        var info = await InspectAsync(input,cancellationToken);
        double duration = excerpt.Validate(info.DurationSeconds);
        string temporary=Path.Combine(Path.GetTempPath(),$"soundscript-transcribe-{Guid.NewGuid():N}.wav");
        try
        {
            // Header duration is rounded by FFmpeg. With no explicit duration, decode to EOF
            // (bounded one second beyond the analysis limit) rather than truncate at that estimate.
            double decodeLimit = excerpt.DurationSeconds.HasValue ? duration : AnalysisAudio.MaximumSeconds + 1;
            string[] arguments=["-nostdin","-hide_banner","-loglevel","error","-y","-i",Path.GetFullPath(input),"-ss",excerpt.StartSeconds.ToString("R",CultureInfo.InvariantCulture),"-map","0:a:0","-vn","-t",decodeLimit.ToString("R",CultureInfo.InvariantCulture),"-ac","1","-ar","16000","-c:a","pcm_s16le",temporary];
            await Task.Run(()=>FfmpegWebmExporter.RunMediaProcess(Executable,arguments,cancellationToken),cancellationToken);
            return PcmWaveInput.Decode(await File.ReadAllBytesAsync(temporary,cancellationToken));
        }
        finally { AtomicOutput.TryDeleteFile(temporary); }
    }
}
