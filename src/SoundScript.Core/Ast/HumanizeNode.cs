namespace SoundScript.Core.Ast;

public sealed record HumanizeNode : AstNode
{
    public required double Value { get; init; }
}
