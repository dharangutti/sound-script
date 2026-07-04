namespace SoundScript.Core.Ast;

/// <summary>
/// Top-level vocal track. Runs on a parallel timeline (like <see cref="TrackNode"/>)
/// but carries lyric-bound pitches instead of instrumental notes.
/// </summary>
public sealed record VoiceNode : AstNode
{
    public required string Name { get; init; }
    public List<AstNode> Body { get; } = [];
}
