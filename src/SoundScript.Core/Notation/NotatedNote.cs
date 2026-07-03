namespace SoundScript.Core.Notation;

/// <summary>
/// Canonical internal representation of a single notated pitch.
/// Duration and start time are populated as the note moves through parse and interpret.
/// </summary>
public sealed record NotatedNote
{
    public PitchClass PitchClass { get; init; }
    public AccidentalType Accidental { get; init; }
    public int Octave { get; init; }
    public NoteDuration? StandardDuration { get; init; }
    public double DurationBeats { get; init; } = 1.0;
    public double StartTime { get; set; }
    public ArticulationType? Articulation { get; init; }
    public DynamicLevel? Dynamic { get; init; }
    public bool IsTied { get; init; }
    public int? AdjustedOctave { get; init; }
    public int? AdjustedMidiNumber { get; init; }
    public int PhraseIndex { get; init; }

    public int EffectiveOctave => AdjustedOctave ?? Octave;

    public bool MatchesPitch(NotatedNote other) =>
        PitchClass == other.PitchClass
        && Accidental == other.Accidental
        && Octave == other.Octave;

    public int ResolvedMidiNumber => AdjustedMidiNumber ?? ToMidiNumber();

    public int ToMidiNumber() => ToMidiNumber(EffectiveOctave);

    public int ToMidiNumber(int octave)
    {
        var pitchClass = GetPitchClassValue();
        return (octave + 1) * 12 + pitchClass;
    }

    public NotatedNote WithAdjustedMidi(int midiNumber)
    {
        var pitchClass = GetPitchClassValue();
        var octave = (midiNumber - pitchClass) / 12 - 1;
        return this with
        {
            AdjustedOctave = octave,
            AdjustedMidiNumber = midiNumber
        };
    }

    private int GetPitchClassValue()
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

        return (pitchClass % 12 + 12) % 12;
    }
}
