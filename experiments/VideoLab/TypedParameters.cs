using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace VideoLab;

// Typed declarations are additive: legacy decimal declarations/API retain their shape.
public sealed record TypedParameter(string Type, JsonElement Default, ImmutableArray<string> Values = default);
public sealed record LayerGroup(string? When = null);
public static class TypedParameters
{
    public static bool Identifier(string? name) => name != null && Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,63}$") && !Expressions.Builtins.Contains(name) && name is not "true" and not "false";
    public static void Validate(Script script)
    {
        var declarations = script.TypedParameters ?? ImmutableDictionary<string, TypedParameter>.Empty;
        Composition.Require(script.Parameters.Count + declarations.Count <= 64, "Limit: 64 total parameters.");
        foreach (var (name, p) in declarations)
        {
            Composition.Require(Identifier(name) && !script.Parameters.ContainsKey(name) && p != null, "Invalid or duplicate typed parameter.");
            Composition.Require(p.Type is "enum" or "boolean", "Typed parameters support enum and boolean only.");
            if (p.Type == "enum") Composition.Require(!p.Values.IsDefault && p.Values.Length is > 0 and <= 32 && p.Values.All(Identifier) && p.Values.Distinct(StringComparer.Ordinal).Count() == p.Values.Length, "Enum requires 1–32 unique identifier values.");
            else Composition.Require(p.Values.IsDefault, "Boolean declarations cannot have enum values.");
            ValidateValue(name, p, p.Default);
        }
    }
    public static void ValidateValue(string name, TypedParameter p, JsonElement value)
    {
        Composition.Require(p.Type == "boolean" ? value.ValueKind is JsonValueKind.True or JsonValueKind.False : value.ValueKind == JsonValueKind.String && p.Values.Contains(value.GetString()!, StringComparer.Ordinal), $"Invalid typed value for '{name}'.");
    }
    public static ImmutableDictionary<string, JsonElement> Defaults(Script s) => (s.TypedParameters ?? ImmutableDictionary<string, TypedParameter>.Empty).ToImmutableDictionary(p => p.Key, p => p.Value.Default.Clone(), StringComparer.Ordinal);
}

public sealed record BooleanLiteral(bool Boolean) : Expression(ValueKind.Boolean)
{ public override Value Evaluate(EvaluationContext context) => Value.Logical(Boolean); }
public sealed record EnumLiteral(string Text) : Expression(ValueKind.Symbol)
{ public override Value Evaluate(EvaluationContext context) => Value.Symbolic(Text); }
public sealed record TypedVariable(string Name, TypedParameter Declaration) : Expression(Declaration.Type == "boolean" ? ValueKind.Boolean : ValueKind.Symbol)
{
    public override Value Evaluate(EvaluationContext context) => Declaration.Type == "boolean" ? Value.Logical(context.Typed![Name].GetBoolean()) : Value.Symbolic(context.Typed![Name].GetString()!);
}
public sealed record LogicalBinary(string Operator, Expression Left, Expression Right) : Expression(ValueKind.Boolean)
{
    public override Value Evaluate(EvaluationContext context) => Value.Logical(Operator == "&&" ? Left.Evaluate(context).Boolean && Right.Evaluate(context).Boolean : Left.Evaluate(context).Boolean || Right.Evaluate(context).Boolean);
}
public sealed record TypedEquality(string Operator, Expression Left, Expression Right) : Expression(ValueKind.Boolean)
{
    public override Value Evaluate(EvaluationContext context)
    {
        var a = Left.Evaluate(context); var b = Right.Evaluate(context);
        var equal = a.Kind == ValueKind.Boolean ? a.Boolean == b.Boolean : string.Equals(a.Text, b.Text, StringComparison.Ordinal);
        return Value.Logical(Operator == "==" ? equal : !equal);
    }
}
