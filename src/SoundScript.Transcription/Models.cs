namespace SoundScript.Transcription;

// Seconds preserve observations; beats describe the editable reconstruction.
public sealed record Evidence(double Confidence, string Method);
public sealed record TempoPoint(double Beat, double Bpm, Evidence Evidence);
public sealed record Meter(int Numerator, int Denominator, Evidence Evidence);
public sealed record KeyEstimate(string Name, Evidence Evidence);
public sealed record Section(string Name, double StartBeat, double EndBeat);
public sealed record ExpressionObservation(string Kind, double Value, Evidence Evidence);
public sealed record MusicalNote(int MidiPitch, double StartSeconds, double DurationSeconds,
    double StartBeat, double DurationBeats, int Velocity, Evidence PitchEvidence,
    IReadOnlyList<ExpressionObservation>? Expression = null);
public sealed record MusicalRest(double StartBeat, double DurationBeats);
public sealed record MusicalChord(double StartBeat, double DurationBeats, IReadOnlyList<int> Pitches, Evidence Evidence);
public sealed record MusicalTrack(string Name, string Role, int Instrument,
    IReadOnlyList<MusicalNote> Notes, IReadOnlyList<MusicalRest> Rests,
    IReadOnlyList<MusicalChord>? Chords = null, string? LegitimateText = null);
public sealed record MusicalScore(IReadOnlyList<TempoPoint> TempoMap, Meter? Meter, KeyEstimate? Key,
    IReadOnlyList<MusicalTrack> Tracks, IReadOnlyList<Section> Sections, double DurationSeconds);
public sealed record AnalysisDiagnostic(string Code, string Message);
public sealed record PitchFrame(double Seconds, double? Frequency, double Periodicity, double Rms, double? FundamentalShare = null);
public sealed record MusicalObservations(IReadOnlyList<PitchFrame> Frames, double DurationSeconds,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics);
public sealed record TranscriptionResult(MusicalScore Score, MusicalObservations Observations,
    double PitchConfidence, double TimingGridFit, IReadOnlyList<AnalysisDiagnostic> Diagnostics)
{
    public TranscriptionSuitability Suitability => TranscriptionSuitability.Evaluate(this);
}
public sealed record TranscriptionOptions(int? Tempo = null, int Instrument = 73, bool Quantize = true);

public interface ITranscriptionInputAdapter<in T>
{
    Task<AnalysisAudio> DecodeAsync(T input, CancellationToken cancellationToken = default);
}
public interface IMusicalAnalyzer
{
    MusicalObservations Analyze(AnalysisAudio audio, CancellationToken cancellationToken = default);
}
public interface ITranscriptionOutput<out T>
{
    T Write(MusicalScore score);
}

/// <summary>Finite, mono floating-point PCM, [-1,1], at 16 kHz. Maximum 120 seconds.</summary>
public sealed class AnalysisAudio
{
    public const int SampleRate = 16000;
    public const int MaximumSeconds = 120;
    public float[] Samples { get; }
    public AnalysisAudio(float[] samples)
    {
        if (samples.Length > SampleRate * MaximumSeconds) throw new InvalidDataException("Audio exceeds the 120-second analysis limit.");
        if (samples.Any(s => !float.IsFinite(s) || Math.Abs(s) > 1.0001f)) throw new InvalidDataException("PCM must contain finite samples in [-1,1].");
        Samples = (float[])samples.Clone();
    }
}
