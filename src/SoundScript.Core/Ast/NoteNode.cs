using SoundScript.Core.Notation;

namespace SoundScript.Core.Ast;

public sealed record NoteNode : AstNode
{
    public NotatedNote Notation { get; init; } = NotationParserDefaults.DefaultNote();

    public char Pitch => Notation.PitchClass switch
    {
        PitchClass.C => 'C',
        PitchClass.D => 'D',
        PitchClass.E => 'E',
        PitchClass.F => 'F',
        PitchClass.G => 'G',
        PitchClass.A => 'A',
        PitchClass.B => 'B',
        _ => 'C'
    };

    public bool IsSharp => Notation.Accidental == AccidentalType.Sharp;
    public bool IsFlat => Notation.Accidental == AccidentalType.Flat;
    public int Octave => Notation.Octave;
    public double DurationBeats => Notation.DurationBeats;
    public int? Velocity { get; init; }

    public int ToMidiNumber() => Notation.ToMidiNumber();
}

internal static class NotationParserDefaults
{
    public static NotatedNote DefaultNote() => new()
    {
        PitchClass = PitchClass.C,
        Octave = 4,
        DurationBeats = 1.0
    };
}
