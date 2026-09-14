using SoundScript.Core.Notation;

namespace SoundScript.Core.Ast;

public sealed record ChordNode : AstNode
{
    public char Root { get; init; }
    public bool IsSharp { get; init; }
    public bool IsFlat { get; init; }
    public ChordQuality Quality { get; init; }
    public int Octave { get; init; } = 4;
    public double DurationBeats { get; init; } = 1.0;
    public int? Velocity { get; init; }
    public ChordVoicingStyle? Voicing { get; init; }

    public IReadOnlyList<int> ToMidiNumbers()
    {
        var rootMidi = BuildRootNotation().ToMidiNumber();

        var intervals = Quality switch
        {
            ChordQuality.Major => new[] { 0, 4, 7 },
            ChordQuality.Minor => new[] { 0, 3, 7 },
            ChordQuality.Diminished => new[] { 0, 3, 6 },
            ChordQuality.Augmented => new[] { 0, 4, 8 },
            ChordQuality.Dominant7 => new[] { 0, 4, 7, 10 },
            ChordQuality.Major7 => new[] { 0, 4, 7, 11 },
            ChordQuality.Sus2 => new[] { 0, 2, 7 },
            ChordQuality.Sus4 => new[] { 0, 5, 7 },
            ChordQuality.Major6 => new[] { 0, 4, 7, 9 },
            ChordQuality.Minor6 => new[] { 0, 3, 7, 9 },
            ChordQuality.Diminished7 => new[] { 0, 3, 6, 9 },
            ChordQuality.HalfDiminished7 => new[] { 0, 3, 6, 10 },
            ChordQuality.Minor7 => new[] { 0, 3, 7, 10 },
            ChordQuality.Major9 => new[] { 0, 4, 7, 11, 14 },
            ChordQuality.Minor9 => new[] { 0, 3, 7, 10, 14 },
            ChordQuality.Dominant9 => new[] { 0, 4, 7, 10, 14 },
            ChordQuality.Eleventh => new[] { 0, 4, 7, 10, 14, 17 },
            ChordQuality.Thirteenth => new[] { 0, 4, 7, 10, 14, 17, 21 },
            ChordQuality.Add9 => new[] { 0, 4, 7, 14 },
            _ => throw new InvalidOperationException($"Unknown chord quality: {Quality}")
        };

        return intervals.Select(i => rootMidi + i).ToList();
    }

    private NotatedNote BuildRootNotation() => new()
    {
        PitchClass = Root switch
        {
            'C' or 'c' => PitchClass.C,
            'D' or 'd' => PitchClass.D,
            'E' or 'e' => PitchClass.E,
            'F' or 'f' => PitchClass.F,
            'G' or 'g' => PitchClass.G,
            'A' or 'a' => PitchClass.A,
            'B' or 'b' => PitchClass.B,
            _ => throw new InvalidOperationException($"Invalid chord root: {Root}")
        },
        Accidental = IsSharp ? AccidentalType.Sharp : IsFlat ? AccidentalType.Flat : AccidentalType.None,
        Octave = Octave
    };
}
