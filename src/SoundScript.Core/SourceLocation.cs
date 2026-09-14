using System.Runtime.CompilerServices;
using SoundScript.Core.Ast;

namespace SoundScript.Core;

/// <summary>Sidecar metadata: never participates in AST equality or execution.</summary>
public sealed record SourceLocation(string? File, int Line, int Column)
{
    private static readonly ConditionalWeakTable<AstNode, SourceLocation> Locations = new();
    public static SourceLocation? For(AstNode node) => Locations.TryGetValue(node, out var value) ? value : null;
    public static void Set(AstNode node, SourceLocation location)
    {
        Locations.Remove(node);
        Locations.Add(node, location);
    }
    public static void Attach(Exception exception, AstNode node)
    {
        if (!exception.Data.Contains("SoundScript.Location") && For(node) is { } location)
            exception.Data["SoundScript.Location"] = location;
    }
}
