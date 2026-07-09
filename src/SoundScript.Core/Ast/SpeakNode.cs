// UNDER DEVELOPMENT — v3
namespace SoundScript.Core.Ast;

/// <summary>
/// Phoneme/prosody tone mapping directive:
/// <c>speak "hello world" voice=default seed=7</c>.
///
/// Wave-backend only (grammar-extension gate: MIDI has no phoneme-level pitch
/// concept, and the construct is isolated to SoundScript.Wave's prosody
/// subsystem). The MIDI interpreter rejects this node with a clear error.
/// Same text + same seed always produces the same tone sequence; a null seed
/// is derived deterministically from the text itself — never wall-clock.
/// </summary>
public sealed record SpeakNode : AstNode
{
    public required string Text { get; init; }

    /// <summary>Voice name; v3 supports only "default".</summary>
    public string Voice { get; init; } = "default";

    /// <summary>Explicit tone-variation seed; null derives one from <see cref="Text"/>.</summary>
    public int? Seed { get; init; }

    /// <summary>Optional recorded vocal stem for this phrase (V8). When set, the WAV file is mixed at speak timing instead of synthetic phoneme tones.</summary>
    public string? SamplePath { get; init; }

    /// <summary>Gain applied to <see cref="SamplePath"/> (default 1.0).</summary>
    public double SampleGain { get; init; } = 1.0;
}
