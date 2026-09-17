namespace SoundScript.Transcription;

/// <summary>Selects the analysis strategy used by <see cref="TranscriptionEngine"/>.</summary>
public enum TranscriptionMode { Monophonic, ExtractMelody, Polyphonic, Mixed, Percussion }
/// <summary>Describes a rejected interval in melody extraction output.</summary>
public sealed record RejectedMelodySection(double StartSeconds, double EndSeconds, string Reason);
/// <summary>Diagnostics produced while extracting a stable melody from complex audio.</summary>
public sealed record MelodyExtractionEvidence(int ExtractedNoteCount, double MelodyCoverage,
    double MeanSpectralShare, double CompetingPitchFraction, double OctaveUncertainFraction,
    IReadOnlyList<RejectedMelodySection> RejectedSections);

/// <summary>Runs SoundScript's existing audio-to-code transcription modes.</summary>
/// <remarks>The default mode is monophonic transcription. Analysis is bounded by
/// <see cref="AnalysisAudio.MaximumSeconds"/> and returns confidence and suitability diagnostics
/// alongside the editable musical score.</remarks>
public sealed class TranscriptionEngine
{
    /// <summary>Transcribes an already decoded audio buffer synchronously.</summary>
    /// <param name="audio">Finite mono PCM normalized to the analysis contract.</param>
    /// <param name="options">Quantization, tempo, instrument, and role options, or <see langword="null"/> for defaults.</param>
    /// <param name="mode">Analysis strategy to run.</param>
    /// <param name="cancellationToken">Token checked by analyzers that support cancellation.</param>
    /// <returns>A score, observations, confidence values, and mode-specific diagnostics.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The requested mode is not supported.</exception>
    public TranscriptionResult Transcribe(AnalysisAudio audio, TranscriptionOptions? options = null,
        TranscriptionMode mode = TranscriptionMode.Monophonic, CancellationToken cancellationToken = default)
        => mode switch
        {
            TranscriptionMode.Percussion => new PercussionTranscriber().Transcribe(audio, options, cancellationToken),
            TranscriptionMode.Monophonic => new MonophonicTranscriber().Transcribe(audio, options, cancellationToken),
            TranscriptionMode.ExtractMelody => Finish(new MelodyExtractor().Analyze(audio, cancellationToken), options),
            TranscriptionMode.Polyphonic => PolyphonicTranscriber.Interpret(new PolyphonicAnalyzer().Analyze(audio, cancellationToken), options ?? new(Instrument: 0)),
            TranscriptionMode.Mixed => MixedTranscriber.Interpret(new PolyphonicAnalyzer().Analyze(audio, cancellationToken), options ?? new(Instrument: 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    /// <summary>Transcribes an already decoded audio buffer asynchronously.</summary>
    /// <param name="audio">Finite mono PCM normalized to the analysis contract.</param>
    /// <param name="options">Quantization, tempo, instrument, and role options, or <see langword="null"/> for defaults.</param>
    /// <param name="mode">Analysis strategy to run.</param>
    /// <param name="cancellationToken">Token used to cancel asynchronous analysis.</param>
    /// <returns>A task containing a score, observations, confidence values, and diagnostics.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The requested mode is not supported.</exception>
    public async Task<TranscriptionResult> TranscribeAsync(AnalysisAudio audio, TranscriptionOptions? options = null,
        TranscriptionMode mode = TranscriptionMode.Monophonic, CancellationToken cancellationToken = default)
    {
        if (mode == TranscriptionMode.Percussion)
            return await new PercussionTranscriber().TranscribeAsync(audio, options, cancellationToken);
        if (mode == TranscriptionMode.Mixed)
            return MixedTranscriber.Interpret(await new PolyphonicAnalyzer().AnalyzeAsync(audio, cancellationToken), options ?? new(Instrument: 0));
        if (mode == TranscriptionMode.Polyphonic)
            return PolyphonicTranscriber.Interpret(await new PolyphonicAnalyzer().AnalyzeAsync(audio, cancellationToken), options ?? new(Instrument: 0));
        if (mode == TranscriptionMode.ExtractMelody)
            return Finish(await new MelodyExtractor().AnalyzeAsync(audio, cancellationToken), options);
        if (mode != TranscriptionMode.Monophonic) throw new ArgumentOutOfRangeException(nameof(mode));
        return new MonophonicTranscriber().Interpret(await new MonophonicAnalyzer().AnalyzeAsync(audio, cancellationToken), options ?? new());
    }

    private static TranscriptionResult Finish(MelodyAnalysis analysis, TranscriptionOptions? options)
    {
        var result = new MonophonicTranscriber().Interpret(analysis.Observations, options ?? new());
        return result with { Extraction = analysis.Evidence with {
            ExtractedNoteCount = result.Score.Tracks.Sum(t => t.Notes.Count),
            MelodyCoverage = result.Suitability.StableActiveFraction
        } };
    }
}
