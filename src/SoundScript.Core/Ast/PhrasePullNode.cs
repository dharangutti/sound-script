namespace SoundScript.Core.Ast;

public sealed record PhrasePullNode : AstNode
{
    public double Beats { get; init; }
}
