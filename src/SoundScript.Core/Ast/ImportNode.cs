namespace SoundScript.Core.Ast;

public sealed record ImportNode : AstNode
{
    public required string Path { get; init; }
}
