using SoundScript.Core.Ast;
using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Expands pattern definitions into individual notes.</summary>
internal static class PatternExpander
{
    private const double StrumStaggerBeats = 0.05;

    internal static IReadOnlyList<NoteNode> Expand(PatternNode pattern, ChordNode chord)
    {
        var midiNotes = PrepareChordNotes(chord);
        var ordered = OrderNotes(midiNotes, pattern.Direction);

        return pattern.Kind switch
        {
            PatternKind.Arpeggio => ExpandArpeggio(ordered, chord.DurationBeats, chord.Velocity),
            PatternKind.Rhythm => ExpandRhythm(ordered, pattern.RhythmBeats, chord.Velocity),
            PatternKind.Strum => ExpandStrum(ordered, chord.DurationBeats, chord.Velocity),
            _ => ExpandArpeggio(ordered, chord.DurationBeats, chord.Velocity)
        };
    }

    internal static double GetTotalDuration(PatternNode pattern, ChordNode chord, IReadOnlyList<NoteNode> notes)
    {
        if (pattern.Kind == PatternKind.Strum)
            return chord.DurationBeats;

        return notes.Sum(note => note.DurationBeats);
    }

    internal static double GetStrumStaggerBeats() => StrumStaggerBeats;

    private static List<int> PrepareChordNotes(ChordNode chord)
    {
        var raw = chord.ToMidiNumbers();
        var (voiced, _) = ChordVoicing.Apply(raw);
        var (advanced, _) = AdvancedChordVoicing.Apply(voiced, chord.Voicing);
        var (spaced, _) = HarmonicSpacing.Apply(advanced);
        return spaced.OrderBy(note => note).ToList();
    }

    private static List<int> OrderNotes(IReadOnlyList<int> ascending, PatternDirection direction)
    {
        if (direction == PatternDirection.Down)
            return ascending.OrderByDescending(note => note).ToList();

        if (direction == PatternDirection.UpDown)
            return BuildUpDownOrder(ascending);

        return ascending.ToList();
    }

    private static List<int> BuildUpDownOrder(IReadOnlyList<int> ascending)
    {
        var ordered = ascending.ToList();
        for (var i = ascending.Count - 2; i >= 0; i--)
            ordered.Add(ascending[i]);

        return ordered;
    }

    private static List<NoteNode> ExpandArpeggio(IReadOnlyList<int> midiNotes, double totalDuration, int? velocity)
    {
        if (midiNotes.Count == 0)
            return [];

        var noteDuration = totalDuration / midiNotes.Count;
        return midiNotes
            .Select(midi => CreateNote(midi, noteDuration, velocity))
            .ToList();
    }

    private static List<NoteNode> ExpandRhythm(
        IReadOnlyList<int> midiNotes,
        IReadOnlyList<double> rhythmBeats,
        int? velocity)
    {
        if (midiNotes.Count == 0 || rhythmBeats.Count == 0)
            return [];

        var notes = new List<NoteNode>(midiNotes.Count);
        for (var i = 0; i < midiNotes.Count; i++)
        {
            var duration = rhythmBeats[i % rhythmBeats.Count];
            notes.Add(CreateNote(midiNotes[i], duration, velocity));
        }

        return notes;
    }

    private static List<NoteNode> ExpandStrum(IReadOnlyList<int> midiNotes, double duration, int? velocity) =>
        midiNotes.Select(midi => CreateNote(midi, duration, velocity)).ToList();

    private static NoteNode CreateNote(int midiNumber, double durationBeats, int? velocity)
    {
        var notation = new NotatedNote
        {
            PitchClass = GetPitchClass(midiNumber),
            Accidental = GetAccidental(midiNumber),
            Octave = midiNumber / 12 - 1,
            DurationBeats = durationBeats
        };

        return new NoteNode { Notation = notation, Velocity = velocity };
    }

    private static PitchClass GetPitchClass(int midiNumber)
    {
        return (midiNumber % 12) switch
        {
            0 => PitchClass.C,
            1 => PitchClass.C,
            2 => PitchClass.D,
            3 => PitchClass.D,
            4 => PitchClass.E,
            5 => PitchClass.F,
            6 => PitchClass.F,
            7 => PitchClass.G,
            8 => PitchClass.G,
            9 => PitchClass.A,
            10 => PitchClass.A,
            11 => PitchClass.B,
            _ => PitchClass.C
        };
    }

    private static AccidentalType GetAccidental(int midiNumber) =>
        (midiNumber % 12) switch
        {
            1 or 3 or 6 or 8 or 10 => AccidentalType.Sharp,
            _ => AccidentalType.None
        };
}
