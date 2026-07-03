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

    public IReadOnlyList<int> ToMidiNumbers()
    {
        var rootMidi = new NoteNode
        {
            Pitch = Root,
            IsSharp = IsSharp,
            IsFlat = IsFlat,
            Octave = Octave
        }.ToMidiNumber();

        var intervals = Quality switch
        {
            ChordQuality.Major => new[] { 0, 4, 7 },
            ChordQuality.Minor => new[] { 0, 3, 7 },
            ChordQuality.Diminished => new[] { 0, 3, 6 },
            ChordQuality.Augmented => new[] { 0, 4, 8 },
            ChordQuality.Dominant7 => new[] { 0, 4, 7, 10 },
            ChordQuality.Major7 => new[] { 0, 4, 7, 11 },
            _ => throw new InvalidOperationException($"Unknown chord quality: {Quality}")
        };

        return intervals.Select(i => rootMidi + i).ToList();
    }
}
