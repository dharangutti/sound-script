namespace SoundScript.Core.Ast;

public sealed record LoopNode : AstNode
{
    public int Count { get; init; }
    public List<AstNode> Body { get; } = [];
}
