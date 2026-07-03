namespace SoundScript.Core.Ast;

public sealed record PhraseCurveNode : AstNode
{
    public PhraseCurveType Curve { get; init; }
}
