namespace SoundScript.Core.Ast;

public sealed record PlayNode : AstNode
{
    public required string SequenceName { get; init; }
    public ChordNode? PatternChord { get; init; }
}
