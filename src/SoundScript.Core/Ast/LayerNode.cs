namespace SoundScript.Core.Ast;

public sealed record LayerNode : AstNode
{
    public required string Name { get; init; }
    public int ProgramNumber { get; init; }
}
