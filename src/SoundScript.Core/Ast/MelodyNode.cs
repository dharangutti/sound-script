namespace SoundScript.Core.Ast;

public sealed record MelodyNode : AstNode
{
    public List<AstNode> Body { get; } = [];
}
