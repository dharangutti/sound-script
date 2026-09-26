using System.Collections.Immutable;
using System.Text.Json;

namespace VideoLab;

public enum InterpolationKind { Linear, EaseIn, EaseOut, EaseInOut, Step }
public sealed record Keyframe(int Frame, Expression Value, InterpolationKind Interpolation);
public abstract record Scalar
{
    public abstract decimal Evaluate(EvaluationContext context);
    public static Scalar Fixed(decimal n) => new ExpressionScalar(new Literal(n));
    public static Scalar Compile(JsonElement value, int frames, IEnumerable<string> parameters, IReadOnlyDictionary<string, Expression>? aliases = null)
    {
        if (value.ValueKind != JsonValueKind.Object) return new ExpressionScalar(ReadExpression(value, parameters, aliases));
        JsonRules.Members(value, "keys");
        var keys = value.GetProperty("keys");
        Composition.Require(keys.ValueKind == JsonValueKind.Array && keys.GetArrayLength() is > 0 and <= 64, "Animation needs 1–64 keyframes.");
        var result = ImmutableArray.CreateBuilder<Keyframe>();
        foreach (var key in keys.EnumerateArray())
        {
            JsonRules.Members(key, "frame", "value", "easing");
            int frame = key.GetProperty("frame").GetInt32();
            Composition.Require(frame >= 0 && frame < frames && (result.Count == 0 || result[^1].Frame < frame), "Keyframes must be strictly increasing within the layer lifetime.");
            var easing = key.TryGetProperty("easing", out var e) ? e.GetString() : "linear";
            var kind = easing switch
            {
                "linear" => InterpolationKind.Linear, "ease-in" => InterpolationKind.EaseIn,
                "ease-out" => InterpolationKind.EaseOut, "ease-in-out" => InterpolationKind.EaseInOut,
                "step" => InterpolationKind.Step, _ => throw new ArgumentException($"Unknown easing '{easing}'.")
            };
            result.Add(new(frame, ReadExpression(key.GetProperty("value"), parameters, aliases), kind));
        }
        return new Animation(result.ToImmutable());
    }
    internal static Expression ReadExpression(JsonElement value, IEnumerable<string> parameters, IReadOnlyDictionary<string, Expression>? aliases)
    {
        var expr = value.ValueKind switch
        {
            JsonValueKind.Number => new Literal(value.GetDecimal()),
            JsonValueKind.String => Expressions.Parse(value.GetString()!, parameters, aliases),
            _ => throw new ArgumentException("Expected a number, expression string, or keyframe object.")
        };
        Composition.Require(expr.Kind == ValueKind.Number, "Property values must be numeric."); return expr;
    }
}
public sealed record ExpressionScalar(Expression Expression) : Scalar
{ public override decimal Evaluate(EvaluationContext context) => Expression.Evaluate(context).Number; }
public sealed record Animation(ImmutableArray<Keyframe> Keys) : Scalar
{
    public override decimal Evaluate(EvaluationContext context)
    {
        // Endpoint expressions are evaluated at their keyframe, never at a mutable playback cursor.
        decimal At(Keyframe k) => k.Value.Evaluate(context with { Frame = k.Frame }).Number;
        if (context.Frame <= Keys[0].Frame) return At(Keys[0]);
        for (int i = 1; i < Keys.Length; i++)
        {
            var a = Keys[i - 1]; var b = Keys[i];
            if (context.Frame >= b.Frame) continue;
            decimal t = (decimal)(context.Frame - a.Frame) / (b.Frame - a.Frame);
            t = a.Interpolation switch
            {
                InterpolationKind.Linear => t, InterpolationKind.EaseIn => t * t,
                InterpolationKind.EaseOut => 1 - (1 - t) * (1 - t),
                InterpolationKind.EaseInOut => t < 0.5m ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t),
                InterpolationKind.Step => 0, _ => throw new InvalidOperationException()
            };
            return At(a) + (At(b) - At(a)) * t;
        }
        return At(Keys[^1]);
    }
}
internal static class JsonRules
{
    internal static void Members(JsonElement element, params string[] allowed)
    {
        Composition.Require(element.ValueKind == JsonValueKind.Object, "Expected an object.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in element.EnumerateObject())
            Composition.Require(allowed.Contains(member.Name, StringComparer.Ordinal) && names.Add(member.Name), $"Unknown or duplicate JSON member '{member.Name}'.");
    }
    internal static void NoDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in element.EnumerateObject()) { Composition.Require(names.Add(member.Name), $"Duplicate JSON member '{member.Name}'."); NoDuplicates(member.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) NoDuplicates(item);
    }
}
