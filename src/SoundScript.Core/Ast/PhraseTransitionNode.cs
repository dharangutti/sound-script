namespace SoundScript.Core.Ast;

public sealed record PhraseTransitionNode : AstNode
{
    public PhraseTransitionMode Mode { get; init; }
}
