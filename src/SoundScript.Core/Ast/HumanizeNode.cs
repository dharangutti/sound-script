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
/// sets the explicit parameters; <see cref="Timing"/>/<see cref="VelocityAmount"/>
/// are independently optional (at least one is required). The MIDI backend
/// ignores <see cref="Seed"/>: it has its own process-level seed mechanism
/// (HumanizeApplicator.SetSeed) that predates grammar-level seeds.</item>
/// </list>
///
/// Consumers should call <see cref="Resolve"/> rather than reading
/// <see cref="Timing"/>/<see cref="VelocityAmount"/>/<see cref="Value"/>
/// directly, so the bare-number vs. named-form fallback logic lives in one
/// place instead of being re-derived (and re-broken) at each call site.
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

    /// <summary>
    /// Resolves the effective timing/velocity jitter magnitudes for either
    /// form. In the bare-number form (<see cref="Timing"/> and
    /// <see cref="VelocityAmount"/> both null) <see cref="Value"/> applies to
    /// both channels, matching v1 behavior. In the named form, each channel
    /// uses its own explicit magnitude and defaults to 0 (not <see cref="Value"/>)
    /// when omitted, so e.g. <c>humanize timing=0.05</c> alone never implies
    /// velocity jitter.
    /// </summary>
    public (double TimingSeconds, double VelocityAmount) Resolve() =>
        Timing is null && VelocityAmount is null
            ? (Value, Value)
            : (Timing ?? 0.0, VelocityAmount ?? 0.0);
}
