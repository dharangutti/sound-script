namespace SoundScript.Core.Ast;

/// <summary>Selects the General MIDI vocal timbre for a voice block: <c>vocal choir</c>.</summary>
public sealed record VocalTimbreNode : AstNode
{
    public required string Name { get; init; }
    public required int ProgramNumber { get; init; }
}
