namespace SoundScript.Core.Ast;

public sealed record PatternNode : AstNode
{
    public required string Name { get; init; }
    public PatternKind Kind { get; init; } = PatternKind.Arpeggio;
    public PatternDirection Direction { get; init; } = PatternDirection.Up;
    public List<double> RhythmBeats { get; } = [];
}
