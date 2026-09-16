namespace SoundScript.Transcription;

public enum TranscriptionMode { Monophonic, ExtractMelody, Polyphonic, Mixed }
public sealed record RejectedMelodySection(double StartSeconds, double EndSeconds, string Reason);
public sealed record MelodyExtractionEvidence(int ExtractedNoteCount, double MelodyCoverage,
    double MeanSpectralShare, double CompetingPitchFraction, double OctaveUncertainFraction,
    IReadOnlyList<RejectedMelodySection> RejectedSections);

/// <summary>Shared desktop/browser dispatch. Default analysis remains the V13 implementation.</summary>
public sealed class TranscriptionEngine
{
    public TranscriptionResult Transcribe(AnalysisAudio audio, TranscriptionOptions? options = null,
        TranscriptionMode mode = TranscriptionMode.Monophonic, CancellationToken cancellationToken = default)
        => mode switch
        {
            TranscriptionMode.Monophonic => new MonophonicTranscriber().Transcribe(audio, options, cancellationToken),
            TranscriptionMode.ExtractMelody => Finish(new MelodyExtractor().Analyze(audio, cancellationToken), options),
            TranscriptionMode.Polyphonic => PolyphonicTranscriber.Interpret(new PolyphonicAnalyzer().Analyze(audio, cancellationToken), options ?? new(Instrument: 0)),
            TranscriptionMode.Mixed => MixedTranscriber.Interpret(new PolyphonicAnalyzer().Analyze(audio, cancellationToken), options ?? new(Instrument: 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    public async Task<TranscriptionResult> TranscribeAsync(AnalysisAudio audio, TranscriptionOptions? options = null,
        TranscriptionMode mode = TranscriptionMode.Monophonic, CancellationToken cancellationToken = default)
    {
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
