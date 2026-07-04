namespace SoundScript.Core.Ast;

public sealed record PhraseSwingNode : AstNode
{
    public double Ratio { get; init; }
}
