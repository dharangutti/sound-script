using SoundScript.Core.Notation;

namespace SoundScript.Core.Ast;

public sealed record RestNode : AstNode
{
    public NotatedRest Rest { get; init; } = new();
}
