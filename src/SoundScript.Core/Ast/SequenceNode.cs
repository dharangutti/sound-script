namespace SoundScript.Core.Ast;

public sealed record SequenceNode : AstNode
{
    public required string Name { get; init; }
    public List<AstNode> Body { get; } = [];
}
