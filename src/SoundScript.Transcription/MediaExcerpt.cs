namespace SoundScript.Transcription;

public sealed record MediaInfo(double DurationSeconds, int SampleRate, int Channels);
public sealed record MediaExcerpt(double StartSeconds = 0, double? DurationSeconds = null)
{
    public double Validate(double mediaDuration)
    {
        if (!double.IsFinite(mediaDuration) || mediaDuration <= 0) throw new InvalidDataException("Media has no measurable audio duration.");
        if (!double.IsFinite(StartSeconds) || StartSeconds < 0 || StartSeconds >= mediaDuration)
            throw new InvalidDataException("Excerpt start must be within the media duration.");
        double duration = DurationSeconds ?? mediaDuration - StartSeconds;
        if (!double.IsFinite(duration) || duration <= 0) throw new InvalidDataException("Excerpt duration must be positive and finite.");
        if (duration > AnalysisAudio.MaximumSeconds)
            throw new InvalidDataException($"Media duration {mediaDuration:F3} seconds; selected duration {duration:F3} seconds exceeds the 120-second analysis limit. Select an excerpt of at most 120 seconds (--start / --duration in the CLI).");
        if (StartSeconds + duration > mediaDuration + .001) throw new InvalidDataException("The selected excerpt extends beyond the media duration.");
        return Math.Min(duration, mediaDuration - StartSeconds);
    }
}
