// UNDER DEVELOPMENT — v3
namespace SoundScript.Core.Ast;

/// <summary>
/// A master-effects directive, e.g. <c>effect delay time=0.25 feedback=0.4 mix=0.3</c>
/// or <c>effect filter type=lowpass cutoff=2000</c>.
///
/// Wave-backend only (grammar-extension gate: MIDI has no post-processing
/// stage, and the construct is fully isolated to SoundScript.Wave, which
/// applies it to the master buffer after the final mix-down). The MIDI
/// interpreter rejects this node with a clear error rather than acting on it.
///
/// The node stays deliberately dumb — kind plus raw string parameters — so
/// SoundScript.Core carries no wave DSP semantics; the parser validates
/// kinds/keys/ranges at parse time and SoundScript.Wave converts to typed
/// effect settings.
/// </summary>
public sealed record EffectNode : AstNode
{
    /// <summary>Lower-cased effect kind: "delay" or "filter" in v3.</summary>
    public required string Kind { get; init; }

    /// <summary>Raw key=value parameters (keys lower-cased by the parser).</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
}
