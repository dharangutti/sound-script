using SoundScript.Media;

namespace SoundScript.Transcription;

/// <summary>Desktop adapter; browser callers provide decoded PCM instead of invoking a process.</summary>
public sealed class DesktopMediaInput(string? ffmpeg = null) : ITranscriptionInputAdapter<string>
{
    public async Task<AnalysisAudio> DecodeAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(input)) throw new FileNotFoundException("Media input was not found.",input);
        string extension=Path.GetExtension(input).ToLowerInvariant();
        if (!new[]{".wav",".mp3",".mp4",".m4a",".webm",".ogg",".flac",".aac",".mov"}.Contains(extension))
            throw new NotSupportedException($"Unsupported media extension '{extension}'. Use WAV, MP3, MP4, M4A, WebM, Ogg, FLAC, AAC or MOV.");
        if (new FileInfo(input).Length>64*1024*1024) throw new InvalidDataException("Media exceeds the 64 MiB input limit.");
        if (extension==".wav")
        {
            try { return PcmWaveInput.Decode(await File.ReadAllBytesAsync(input,cancellationToken)); }
            catch (NotSupportedException) { /* Other WAV encodings use the existing media process boundary. */ }
        }
        string temporary=Path.Combine(Path.GetTempPath(),$"soundscript-transcribe-{Guid.NewGuid():N}.wav");
        try
        {
            string executable=ffmpeg ?? Environment.GetEnvironmentVariable("SOUNDSCRIPT_FFMPEG") ?? "ffmpeg";
            string[] arguments=["-nostdin","-hide_banner","-loglevel","error","-y","-i",Path.GetFullPath(input),"-map","0:a:0","-vn","-t","121","-ac","1","-ar","16000","-c:a","pcm_s16le",temporary];
            await Task.Run(()=>FfmpegWebmExporter.RunMediaProcess(executable,arguments,cancellationToken),cancellationToken);
            return PcmWaveInput.Decode(await File.ReadAllBytesAsync(temporary,cancellationToken));
        }
        finally { AtomicOutput.TryDeleteFile(temporary); }
    }
}
