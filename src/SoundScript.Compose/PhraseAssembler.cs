using SoundScript.Core.Ast;

namespace SoundScript.Compose;

/// <summary>
/// Accumulates musical gestures into SoundScript phrases and assembles the
/// final AST. Each syllable becomes one <see cref="PhraseNode"/> (a musical
/// micro-phrase), so the existing phrase machinery — envelopes, articulation
/// shaping, phrase smoothing — shapes the output exactly as it would for a
/// hand-written script. Appending the same gestures in the same order always
/// yields a structurally identical AST.
/// </summary>
public sealed class PhraseAssembler
{
    /// <summary>Name of the track the assembled phrases play on.</summary>
    public const string TrackName = "phonemes";

    private readonly List<PhraseNode> _phrases = [];
    private List<NoteNode>? _currentNotes;
    private PhraseEnvelopeNode? _currentEnvelope;

    /// <summary>Opens a new syllable phrase.</summary>
    public void BeginSyllable()
    {
        FlushSyllable();
        _currentNotes = [];
    }

    /// <summary>Appends one gesture to the current syllable phrase.</summary>
    public void Append(MusicalGesture gesture)
    {
        _currentNotes ??= [];
        _currentNotes.Add(GestureBuilder.BuildNote(gesture));

        // the first swell/fade in a syllable decides its phrase envelope
        _currentEnvelope ??= GestureBuilder.BuildEnvelope(gesture.Kind);
    }

    /// <summary>Closes the current syllable phrase.</summary>
    public void EndSyllable() => FlushSyllable();

    /// <summary>
    /// Assembles the accumulated phrases into a complete program AST: a tempo
    /// statement followed by one track holding every syllable phrase.
    /// </summary>
    public ProgramNode BuildProgram(int tempo)
    {
        FlushSyllable();

        var track = new TrackNode { Name = TrackName };
        track.Body.AddRange(_phrases);

        var program = new ProgramNode();
        program.Statements.Add(new TempoNode { Bpm = tempo });
        program.Statements.Add(track);
        return program;
    }

    private void FlushSyllable()
    {
        if (_currentNotes is { Count: > 0 } notes)
        {
            var phrase = new PhraseNode();
            if (_currentEnvelope is not null)
                phrase.Body.Add(_currentEnvelope);
            phrase.Body.AddRange(notes);
            _phrases.Add(phrase);
        }

        _currentNotes = null;
        _currentEnvelope = null;
    }
}
