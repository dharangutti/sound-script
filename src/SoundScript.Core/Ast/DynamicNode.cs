using SoundScript.Core.Notation;

namespace SoundScript.Core.Ast;

public sealed record DynamicNode : AstNode
{
    public DynamicLevel Level { get; init; }
}
