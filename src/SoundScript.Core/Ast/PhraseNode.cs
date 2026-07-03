namespace SoundScript.Core.Ast;

public sealed record PhraseNode : AstNode
{
    public List<AstNode> Body { get; } = [];
}
