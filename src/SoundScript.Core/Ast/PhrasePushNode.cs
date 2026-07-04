namespace SoundScript.Core.Ast;

public sealed record PhrasePushNode : AstNode
{
    public double Beats { get; init; }
}
