namespace SoundScript.Core.Ast;

public sealed record ProgramNode : AstNode
{
    public List<AstNode> Statements { get; } = [];
}
