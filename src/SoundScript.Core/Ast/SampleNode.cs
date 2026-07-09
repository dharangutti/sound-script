// V8 — wave-backend vocal stem / recorded sample overlay.
namespace SoundScript.Core.Ast;

/// <summary>
/// Mixes an external WAV file into the wave render at a beat position:
/// <c>sample "vocals/take.wav" gain=0.9 at=0</c>.
///
/// Wave-backend only. Paths are resolved relative to the script file directory.
/// </summary>
public sealed record SampleNode : AstNode
{
    public required string Path { get; init; }

    /// <summary>Linear gain 0.0–1.0+ (default 1.0).</summary>
    public double Gain { get; init; } = 1.0;

    /// <summary>Start beat on the current track; null uses the track cursor.</summary>
    public double? AtBeats { get; init; }
}
