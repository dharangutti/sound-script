namespace SoundScript.Core.Ast;

/// <summary>
/// Advances the visual narrative cursor without creating a visual interval.
/// This is intentionally distinct from musical <see cref="RestNode"/>, which
/// advances a beat-based score cursor.
/// </summary>
public sealed record VisualWaitNode : AstNode
{
    public required TimeSpan Duration { get; init; }
}
