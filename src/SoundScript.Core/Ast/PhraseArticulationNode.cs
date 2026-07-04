using SoundScript.Core.Notation;

namespace SoundScript.Core.Ast;

public sealed record PhraseArticulationNode : AstNode
{
    public ArticulationType Articulation { get; init; }
}
