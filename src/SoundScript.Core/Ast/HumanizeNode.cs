// UNDER DEVELOPMENT — v3: named-parameter extension of the existing
// humanize directive (no new keyword; see the wave safeguards doc's grammar
// extension gate — this deliberately extends a shared construct).
namespace SoundScript.Core.Ast;

/// <summary>
/// The <c>humanize</c> directive, in both of its forms:
///
/// <list type="bullet">
/// <item>v1 bare-number form — <c>humanize 0.02</c> — sets <see cref="Value"/>
/// only; <see cref="Timing"/>/<see cref="VelocityAmount"/>/<see cref="Seed"/>
/// stay null and behavior is unchanged everywhere.</item>
/// <item>v3 named-parameter form — <c>humanize timing=0.02 velocity=0.1 seed=42</c> —
/// sets the explicit parameters, and the parser also sets
/// <see cref="Value"/> to <c>Timing ?? 0.0</c> so the MIDI backend (which
/// consumes the single <see cref="Value"/> as both its timing-seconds and
/// velocity-fraction magnitude) keeps working without modification. The MIDI
/// backend ignores <see cref="Seed"/>: it has its own process-level seed
/// mechanism (HumanizeApplicator.SetSeed) that predates grammar-level seeds.</item>
/// </list>
/// </summary>
public sealed record HumanizeNode : AstNode
{
    public required double Value { get; init; }

    /// <summary>Max timing jitter in seconds (±). Null in the bare-number form.</summary>
    public double? Timing { get; init; }

    /// <summary>Max velocity jitter as a 0.0-1.0 fraction (±). Null in the bare-number form.</summary>
    public double? VelocityAmount { get; init; }

    /// <summary>
    /// Explicit jitter seed. Null means "derive deterministically from file
    /// content" (the wave backend hashes the track name) — never wall-clock.
    /// </summary>
    public int? Seed { get; init; }
}
