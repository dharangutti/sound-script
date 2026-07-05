using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;

namespace SoundScript.Prosody;

/// <summary>
/// Deterministic text → music engine, V5: pitch is planned top-down —
/// phrase → word → syllable — instead of per phoneme category (V3.1's
/// <c>PhonemeComposer</c>, which this leaves entirely unchanged and running
/// side by side):
///
///     text → words (WordTokenizer, syllables via the existing Syllabifier)
///          → word base pitch (WordProsodyPlanner + WordPitchTable)
///          → phrase contour delta (PhraseContourEngine)
///          → syllable micro-pitch (SyllableContourGenerator)
///          → bounded pitch sequence (ProsodyClamp)
///          → phonemes for rhythm/articulation only (PhonemeSplitter + PhonemeMapper.Kind/Duration)
///          → phrases → AST (ProsodyPhraseAssembler)
///          → AST → InterpretedTrack (existing Interpreter)
///          → MIDI (existing MidiGenerator)
///
/// Deterministic, no randomness: identical input text yields identical MIDI
/// bytes on every platform, the same guarantee <c>PhonemeComposer</c> makes.
/// </summary>
public static class ProsodyComposer
{
    public const int DefaultTempo = 96;

    /// <summary>Composes a plain text string into an interpreted track.</summary>
    public static InterpretedTrack Compose(string text, int tempo = DefaultTempo)
    {
        var program = ComposeProgram(text, tempo);
        return program.Tracks.FirstOrDefault(track => track.Name == ProsodyPhraseAssembler.TrackName)
            ?? new InterpretedTrack { Name = ProsodyPhraseAssembler.TrackName };
    }

    /// <summary>
    /// Composes a plain text string into a complete interpreted program
    /// (tempo map included), ready for <c>MidiGenerator.Write</c>.
    /// </summary>
    public static InterpretedProgram ComposeProgram(string text, int tempo = DefaultTempo) =>
        Interpreter.Interpret(BuildAst(text, tempo));

    /// <summary>
    /// Adds the composed track to an existing interpreted program as one more
    /// track, using the host program's tempo.
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
        var assembler = new ProsodyPhraseAssembler();

        var words = WordTokenizer.Tokenize(text);
        if (words.Count > 0)
            ComposeWords(words, text, assembler);

        return assembler.BuildProgram(tempo);
    }

    private static void ComposeWords(IReadOnlyList<WordUnit> words, string text, ProsodyPhraseAssembler assembler)
    {
        var wordPlans = WordProsodyPlanner.Plan(words);
        var sentenceType = PhraseContourEngine.DetectSentenceType(text);
        var phraseDeltas = PhraseContourEngine.ComputeDeltas(words.Count, sentenceType);

        // Flatten every syllable's raw target pitch, in word/syllable order,
        // so the clamp pass can see the whole phrase's sequence at once.
        var rawTargets = new List<int>();
        var syllableTexts = new List<string>();

        for (var w = 0; w < words.Count; w++)
        {
            var wordTarget = wordPlans[w].BaseMidi + phraseDeltas[w];
            var offsets = SyllableContourGenerator.GenerateOffsets(wordPlans[w].Stress);
            var syllables = words[w].Syllables;

            for (var s = 0; s < syllables.Count; s++)
            {
                rawTargets.Add(wordTarget + offsets[s]);
                syllableTexts.Add(syllables[s]);
            }
        }

        var clamped = ProsodyClamp.Clamp(rawTargets);

        for (var i = 0; i < syllableTexts.Count; i++)
        {
            var (pitchClass, accidental, octave) = PitchMath.FromMidiNumber(clamped[i]);

            assembler.BeginSyllable();
            foreach (var phoneme in PhonemeSplitter.Split(syllableTexts[i]))
            {
                var gesture = PhonemeMapper.Map(phoneme);
                assembler.AppendPhoneme(gesture.Kind, pitchClass, accidental, octave, gesture.Duration);
            }

            assembler.EndSyllable();
        }
    }
}
