namespace SoundScript.Core.Ast;

public sealed record GainNode : AstNode
{
    public required double Value { get; init; }
}
