namespace SoundScript.Core.Ast;

public sealed record BlockNode : AstNode
{
    public required string Name { get; init; }
    public List<AstNode> Body { get; } = [];
}
