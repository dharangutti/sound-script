using SoundScript.Compose;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Prosody;

/// <summary>
/// Accumulates prosody-built notes into per-syllable phrases and assembles
/// the final AST — the prosody-pipeline counterpart of
/// <c>SoundScript.Compose.PhraseAssembler</c>. Kept as a separate small type
/// (rather than reusing that one) because its <c>Append</c> only accepts a
/// <see cref="MusicalGesture"/>, whose fixed <c>Pitch</c>/<c>Octave</c> fields
/// can't carry a prosody-resolved, potentially chromatic pitch. Every
/// syllable still becomes one <see cref="PhraseNode"/>, so the same phrase
/// machinery (envelopes, articulation) applies.
/// </summary>
internal sealed class ProsodyPhraseAssembler
{
    /// <summary>Name of the track the assembled phrases play on.</summary>
    public const string TrackName = "prosody";

    private readonly List<PhraseNode> _phrases = [];
    private List<NoteNode>? _currentNotes;
    private PhraseEnvelopeNode? _currentEnvelope;

    /// <summary>Opens a new syllable phrase.</summary>
    public void BeginSyllable()
    {
        FlushSyllable();
        _currentNotes = [];
    }

    /// <summary>Appends one phoneme, resolved to an explicit prosody pitch, to the current syllable phrase.</summary>
    public void AppendPhoneme(
        GestureKind kind,
        PitchClass pitchClass,
        AccidentalType accidental,
        int octave,
        NoteDuration duration)
    {
        _currentNotes ??= [];
        _currentNotes.Add(ProsodyNoteBuilder.BuildNote(kind, pitchClass, accidental, octave, duration));

        // the first swell/fade in a syllable decides its phrase envelope
        _currentEnvelope ??= GestureBuilder.BuildEnvelope(kind);
    }

    /// <summary>Closes the current syllable phrase.</summary>
    public void EndSyllable() => FlushSyllable();

    /// <summary>Assembles the accumulated phrases into a complete program AST.</summary>
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
