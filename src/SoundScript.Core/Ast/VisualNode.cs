namespace SoundScript.Core.Ast;

/// <summary>
/// A named visual interval in the renderer-neutral SoundScript AST.
/// Time is expressed as an exact <see cref="TimeSpan"/>, never as a frame
/// count. A visual renderer turns this semantic interval into frames only at
/// export time.
/// </summary>
public sealed record VisualNode : AstNode
{
    /// <summary>Stable renderer-facing visual identifier, asset name, or text key.</summary>
    public required string Name { get; init; }

    /// <summary>How long this visual is active.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Optional absolute placement from the program's temporal origin. When
    /// absent, the visual interpreter places this node at its narrative cursor.
    /// </summary>
    public TimeSpan? At { get; init; }

    /// <summary>Property automations evaluated over this visual's interval.</summary>
    public List<VisualAutomationNode> Automations { get; } = [];
}

/// <summary>
/// A deterministic linear property curve local to a <see cref="VisualNode"/>.
/// Decimal values preserve authored values exactly until a future renderer
/// chooses its own output representation.
/// </summary>
public sealed record VisualAutomationNode
{
    public required string Property { get; init; }
    public decimal From { get; init; }
    public decimal To { get; init; }
    public required TimeSpan Duration { get; init; }
}
