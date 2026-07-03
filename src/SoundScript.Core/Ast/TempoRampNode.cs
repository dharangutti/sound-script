namespace SoundScript.Core.Ast;

public sealed record TempoRampNode : AstNode
{
    public int StartBpm { get; init; }
    public int EndBpm { get; init; }
    public int Bars { get; init; }
}
