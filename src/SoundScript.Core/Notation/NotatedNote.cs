namespace SoundScript.Core.Notation;

/// <summary>
/// Canonical internal representation of a single notated pitch.
/// Duration and start time are populated as the note moves through parse and interpret.
/// </summary>
public sealed class NotatedNote
{
    public PitchClass PitchClass { get; init; }
    public AccidentalType Accidental { get; init; }
    public int Octave { get; init; }
    public NoteDuration? StandardDuration { get; init; }
    public double DurationBeats { get; init; } = 1.0;
    public double StartTime { get; set; }

    public int ToMidiNumber()
    {
        var pitchClass = PitchClass switch
        {
            PitchClass.C => 0,
            PitchClass.D => 2,
            PitchClass.E => 4,
            PitchClass.F => 5,
            PitchClass.G => 7,
            PitchClass.A => 9,
            PitchClass.B => 11,
            _ => throw new InvalidOperationException($"Unknown pitch class: {PitchClass}")
        };

        pitchClass += Accidental switch
        {
            AccidentalType.Sharp => 1,
            AccidentalType.Flat => -1,
            AccidentalType.None or AccidentalType.Natural => 0,
            _ => throw new InvalidOperationException($"Unknown accidental: {Accidental}")
        };

        pitchClass = (pitchClass % 12 + 12) % 12;
        return (Octave + 1) * 12 + pitchClass;
    }
}
