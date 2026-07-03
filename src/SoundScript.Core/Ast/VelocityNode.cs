namespace SoundScript.Core.Ast;

public sealed record VelocityNode : AstNode
{
    public int Velocity { get; init; }
}
