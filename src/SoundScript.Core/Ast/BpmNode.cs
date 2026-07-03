namespace SoundScript.Core.Ast;

public sealed record BpmNode : AstNode
{
    public int Bpm { get; init; }
}
