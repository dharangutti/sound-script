using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Core.Phonetics;

namespace SoundScript.Compose;

/// <summary>
/// Deterministic text → music engine:
///
///     text → words → syllables (Syllabifier)
///          → phonemes (PhonemeSplitter)
///          → gestures (PhonemeMapper)
///          → phrases (GestureBuilder + PhraseAssembler)
///          → AST → InterpretedTrack (existing Interpreter)
///          → MIDI (existing MidiGenerator)
///
/// A parallel pipeline branch beside the instrumental interpreter and the
/// vocal subsystem — nothing existing is modified. No browser APIs, no speech
/// synthesis, no audio, no randomness: identical input yields identical MIDI
/// bytes on every platform.
/// </summary>
public static class PhonemeComposer
{
    public const int DefaultTempo = 96;

    /// <summary>Composes a plain text string into an interpreted track.</summary>
    public static InterpretedTrack Compose(string text, int tempo = DefaultTempo)
    {
        var program = ComposeProgram(text, tempo);
        return program.Tracks.FirstOrDefault(track => track.Name == PhraseAssembler.TrackName)
            ?? new InterpretedTrack { Name = PhraseAssembler.TrackName };
    }

    /// <summary>
    /// Composes a plain text string into a complete interpreted program
    /// (tempo map included), ready for <c>MidiGenerator.Write</c>.
    /// </summary>
    public static InterpretedProgram ComposeProgram(string text, int tempo = DefaultTempo) =>
        Interpreter.Interpret(BuildAst(text, tempo));

    /// <summary>
    /// Adds the composed track to an existing interpreted program as one more
    /// track, using the host program's tempo. The MIDI generator renders it
    /// like any other track — no generator changes required.
    /// </summary>
    public static void AppendTo(InterpretedProgram program, string text)
    {
        var track = Compose(text, program.Tempo);
        if (track.Notes.Count > 0)
            program.Tracks.Add(track);
    }

    /// <summary>Builds the program AST for a text without interpreting it.</summary>
    public static ProgramNode BuildAst(string text, int tempo = DefaultTempo)
    {
        var assembler = new PhraseAssembler();
        ComposeSyllables(SplitSyllables(text), 0, assembler);
        return assembler.BuildProgram(tempo);
    }

    /// <summary>
    /// Splits text into words and words into syllables via the existing
    /// deterministic <see cref="Syllabifier"/>.
    /// </summary>
    public static IReadOnlyList<string> SplitSyllables(string text)
    {
        var syllables = new List<string>();

        foreach (var word in SplitWords(text))
            syllables.AddRange(Syllabifier.Syllabify(word));

        return syllables;
    }

    /// <summary>
    /// Recursive composition: each call handles one syllable — splitting it
    /// into phonemes, mapping each phoneme to a gesture, appending the
    /// gestures to the assembler — then recurses on the rest.
    /// </summary>
    private static void ComposeSyllables(
        IReadOnlyList<string> syllables,
        int index,
        PhraseAssembler assembler)
    {
        if (index >= syllables.Count)
            return;

        assembler.BeginSyllable();

        foreach (var phoneme in PhonemeSplitter.Split(syllables[index]))
            assembler.Append(PhonemeMapper.Map(phoneme));

        assembler.EndSyllable();
        ComposeSyllables(syllables, index + 1, assembler);
    }

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(text))
            return words;

        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var isWordChar = char.IsLetter(text[i]) || text[i] == '\'';
            if (isWordChar && start < 0)
                start = i;
            else if (!isWordChar && start >= 0)
            {
                words.Add(text[start..i]);
                start = -1;
            }
        }

        if (start >= 0)
            words.Add(text[start..]);

        return words;
    }
}
