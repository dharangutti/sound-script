namespace SoundScript.Core.Ast;

public sealed record PhraseEnvelopeNode : AstNode
{
    public PhraseEnvelopeType Envelope { get; init; }
}
