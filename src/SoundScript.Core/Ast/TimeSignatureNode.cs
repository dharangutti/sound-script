namespace SoundScript.Core.Ast;

public sealed record TimeSignatureNode : AstNode
{
    public int Numerator { get; init; }
    public int Denominator { get; init; }
}
