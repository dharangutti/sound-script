namespace SoundScript.Core.Ast;

public sealed record InstrumentNode : AstNode
{
    public int ProgramNumber { get; init; }
    public string Name { get; init; } = "piano";
}
