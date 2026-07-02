namespace SoundScript.Core;

public readonly record struct ParsedNote(char Pitch, bool IsSharp, bool IsFlat, int Octave, double DurationBeats = 1.0)
{
    public int ToMidiNumber()
    {
        var pitchClass = Pitch switch
        {
            'C' or 'c' => 0,
            'D' or 'd' => 2,
            'E' or 'e' => 4,
            'F' or 'f' => 5,
            'G' or 'g' => 7,
            'A' or 'a' => 9,
            'B' or 'b' => 11,
            _ => throw new InvalidOperationException($"Invalid pitch: {Pitch}")
        };

        if (IsSharp)
            pitchClass++;
        if (IsFlat)
            pitchClass--;

        pitchClass = (pitchClass % 12 + 12) % 12;
        return (Octave + 1) * 12 + pitchClass;
    }
}
