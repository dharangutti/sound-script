namespace SoundScript.Core.Ast;

public sealed record TempoNode : AstNode
{
    public int Bpm { get; init; }
}
