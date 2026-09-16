namespace SoundScript.Core.Ast;

/// <summary>Unpitched hit. Duration advances the cursor; synthesis has its own decay.</summary>
public sealed record HitNode : AstNode
{
    public PercussionSound Sound { get; init; }
    public double DurationBeats { get; init; } = .25;
    public int? Velocity { get; init; }
}
