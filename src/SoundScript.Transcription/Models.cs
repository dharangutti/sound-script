namespace SoundScript.Transcription;

// Seconds preserve observations; beats describe the editable reconstruction.
/// <summary>Confidence and method metadata for a transcription estimate.</summary>
public sealed record Evidence(double Confidence, string Method);
/// <summary>A tempo change in the reconstructed score.</summary>
public sealed record TempoPoint(double Beat, double Bpm, Evidence Evidence);
/// <summary>Time signature estimate for a reconstructed score.</summary>
public sealed record Meter(int Numerator, int Denominator, Evidence Evidence);
/// <summary>Key estimate for a reconstructed score.</summary>
public sealed record KeyEstimate(string Name, Evidence Evidence);
/// <summary>A named interval in a reconstructed score.</summary>
public sealed record Section(string Name, double StartBeat, double EndBeat);
/// <summary>An observed expressive value such as dynamics or articulation.</summary>
public sealed record ExpressionObservation(string Kind, double Value, Evidence Evidence);
/// <summary>A pitched event with source timing and editable beat timing.</summary>
public sealed record MusicalNote(int MidiPitch, double StartSeconds, double DurationSeconds,
    double StartBeat, double DurationBeats, int Velocity, Evidence PitchEvidence,
    IReadOnlyList<ExpressionObservation>? Expression = null);
/// <summary>An empty interval in the reconstructed score.</summary>
public sealed record MusicalRest(double StartBeat, double DurationBeats);
/// <summary>A simultaneous group of pitches detected in the source audio.</summary>
public sealed record MusicalChord(double StartBeat, double DurationBeats, IReadOnlyList<int> Pitches, Evidence Evidence);
/// <summary>A reconstructed musical part, including optional chords, lyrics, and percussion.</summary>
public sealed record MusicalTrack(string Name, string Role, int Instrument,
    IReadOnlyList<MusicalNote> Notes, IReadOnlyList<MusicalRest> Rests,
    IReadOnlyList<MusicalChord>? Chords = null, string? LegitimateText = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PercussionHit>? Percussion { get; init; }
}
/// <summary>Canonical score data produced by transcription.</summary>
public sealed record MusicalScore(IReadOnlyList<TempoPoint> TempoMap, Meter? Meter, KeyEstimate? Key,
    IReadOnlyList<MusicalTrack> Tracks, IReadOnlyList<Section> Sections, double DurationSeconds);
/// <summary>A warning or informational message emitted during analysis.</summary>
public sealed record AnalysisDiagnostic(string Code, string Message);
/// <summary>A single pitch-analysis frame from the normalized audio.</summary>
public sealed record PitchFrame(double Seconds, double? Frequency, double Periodicity, double Rms, double? FundamentalShare = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? SpectralShare { get; init; }
}
/// <summary>Pitch observations retained as evidence for a transcription result.</summary>
public sealed record MusicalObservations(IReadOnlyList<PitchFrame> Frames, double DurationSeconds,
    IReadOnlyList<AnalysisDiagnostic> Diagnostics);
/// <summary>Transcribed score plus confidence, observations, and suitability diagnostics.</summary>
public sealed record TranscriptionResult(MusicalScore Score, MusicalObservations Observations,
    double PitchConfidence, double TimingGridFit, IReadOnlyList<AnalysisDiagnostic> Diagnostics)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MelodyExtractionEvidence? Extraction { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PolyphonicEvidence? Polyphony { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public MixedEvidence? Mixed { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PercussionEvidence? Percussion { get; init; }
    public TranscriptionSuitability Suitability => TranscriptionSuitability.Evaluate(this);
}
/// <summary>Controls reconstruction tempo, instrument, quantization, and mixed-audio roles.</summary>
public sealed record TranscriptionOptions(int? Tempo = null, int Instrument = 73, bool Quantize = true)
{
    /// <summary>Optional role filters used by mixed-audio analysis.</summary>
    public IReadOnlyList<string>? Roles { get; init; }
}

/// <summary>Decodes an application-specific input into normalized analysis audio.</summary>
public interface ITranscriptionInputAdapter<in T>
{
    /// <summary>Decodes the supplied input.</summary>
    /// <param name="input">Input value understood by the adapter.</param>
    /// <param name="cancellationToken">Token used to cancel decoding.</param>
    /// <returns>A task containing normalized analysis audio.</returns>
    Task<AnalysisAudio> DecodeAsync(T input, CancellationToken cancellationToken = default);
}
/// <summary>Analyzes normalized audio into pitch and timing observations.</summary>
public interface IMusicalAnalyzer
{
    /// <summary>Analyzes the supplied audio.</summary>
    /// <param name="audio">Normalized mono analysis audio.</param>
    /// <param name="cancellationToken">Token used to cancel analysis.</param>
    /// <returns>Observations and diagnostics for the analyzed audio.</returns>
    MusicalObservations Analyze(AnalysisAudio audio, CancellationToken cancellationToken = default);
}
/// <summary>Converts a canonical score to an application-facing output type.</summary>
public interface ITranscriptionOutput<out T>
{
    /// <summary>Writes the supplied score.</summary>
    /// <param name="score">Score to convert.</param>
    /// <returns>The requested output representation.</returns>
    T Write(MusicalScore score);
}

/// <summary>Finite, mono floating-point PCM in [-1, 1], sampled at 16 kHz, with a 120-second limit.</summary>
public sealed class AnalysisAudio
{
    /// <summary>The sample rate expected by analyzers.</summary>
    public const int SampleRate = 16000;
    /// <summary>The maximum duration accepted by the built-in analyzers.</summary>
    public const int MaximumSeconds = 120;
    /// <summary>Cloned PCM samples in the range [-1, 1].</summary>
    public float[] Samples { get; }
    /// <summary>Creates an immutable-by-convention analysis buffer from PCM samples.</summary>
    /// <param name="samples">Mono floating-point samples in [-1, 1]. The array is cloned.</param>
    /// <exception cref="InvalidDataException">The samples contain non-finite/out-of-range values or exceed the duration limit.</exception>
    public AnalysisAudio(float[] samples)
    {
        if (samples.Length > SampleRate * MaximumSeconds) throw new InvalidDataException("Audio exceeds the 120-second analysis limit.");
        if (samples.Any(s => !float.IsFinite(s) || Math.Abs(s) > 1.0001f)) throw new InvalidDataException("PCM must contain finite samples in [-1,1].");
        Samples = (float[])samples.Clone();
    }
}
