namespace SoundScript.Core.Ast;

/// <summary>
/// A lyric line bound to a sequence of pitches: <c>sing "Twinkle twinkle" C4 q C4 q G4 q G4 q</c>.
/// Syllable-to-note alignment happens in the vocal interpreter.
/// </summary>
public sealed record SingNode : AstNode
{
    public required string Lyric { get; init; }
    public List<NoteNode> Notes { get; } = [];
}
