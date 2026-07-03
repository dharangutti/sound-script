namespace SoundScript.Core.Ast;

public sealed record OrchestrationNode : AstNode
{
    public OrchestrationType Type { get; init; }
}
