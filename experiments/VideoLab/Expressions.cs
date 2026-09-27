using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace VideoLab;

public enum ValueKind { Number, Boolean, Symbol }
public readonly record struct Value(decimal Number, bool Boolean, ValueKind Kind, string? Text = null)
{
    public static Value Symbolic(string text) => new(0, false, ValueKind.Symbol, text);
    public static Value Numeric(decimal n) => new(n, false, ValueKind.Number);
    public static Value Logical(bool b) => new(0, b, ValueKind.Boolean);
}
public sealed record EvaluationContext(ImmutableDictionary<string, decimal> Parameters, int Frame, int Frames, int Fps, int CanvasWidth, int CanvasHeight, ImmutableDictionary<string, JsonElement>? Typed = null)
{
    public decimal Resolve(string name) => name switch
    {
        "frame" => Frame, "progress" => Frames <= 1 ? 0 : (decimal)Frame / (Frames - 1),
        "fps" => Fps, "canvasWidth" => CanvasWidth, "canvasHeight" => CanvasHeight,
        _ => Parameters[name]
    };
}
public abstract record Expression(ValueKind Kind)
{
    public abstract Value Evaluate(EvaluationContext context);
}
public sealed record Literal(decimal Number) : Expression(ValueKind.Number)
{ public override Value Evaluate(EvaluationContext context) => Value.Numeric(Number); }
public sealed record Variable(string Name) : Expression(ValueKind.Number)
{ public override Value Evaluate(EvaluationContext context) => Value.Numeric(context.Resolve(Name)); }
public sealed record Unary(Expression Operand) : Expression(ValueKind.Number)
{ public override Value Evaluate(EvaluationContext context) => Value.Numeric(-Operand.Evaluate(context).Number); }
public sealed record Binary(string Operator, Expression Left, Expression Right, ValueKind ResultKind) : Expression(ResultKind)
{
    public override Value Evaluate(EvaluationContext context)
    {
        var a = Left.Evaluate(context).Number; var b = Right.Evaluate(context).Number;
        return Operator switch
        {
            "+" => Value.Numeric(a + b), "-" => Value.Numeric(a - b), "*" => Value.Numeric(a * b),
            "/" => b == 0 ? throw new ArgumentException("Expression division by zero.") : Value.Numeric(a / b),
            ">" => Value.Logical(a > b), "<" => Value.Logical(a < b), ">=" => Value.Logical(a >= b),
            "<=" => Value.Logical(a <= b), "==" => Value.Logical(a == b), "!=" => Value.Logical(a != b),
            _ => throw new InvalidOperationException("Invalid compiled operator.")
        };
    }
}
public sealed record Function(string Name, ImmutableArray<Expression> Arguments) : Expression(ValueKind.Number)
{
    public override Value Evaluate(EvaluationContext context)
    {
        var a = Arguments.Select(x => x.Evaluate(context).Number).ToArray();
        return Value.Numeric(Name switch
        {
            "abs" => Math.Abs(a[0]), "min" => Math.Min(a[0], a[1]), "max" => Math.Max(a[0], a[1]),
            "clamp" => a[1] <= a[2] ? Math.Clamp(a[0], a[1], a[2]) : throw new ArgumentException("clamp minimum exceeds maximum."),
            _ => throw new InvalidOperationException("Invalid compiled function.")
        });
    }
}

// A deliberately closed grammar. No reflection, host calls or backend expression strings.
public static class Expressions
{
    public static readonly ImmutableHashSet<string> Builtins = ImmutableHashSet.Create("frame", "progress", "fps", "canvasWidth", "canvasHeight");
    public static Expression Parse(string source, IEnumerable<string> parameters, IReadOnlyDictionary<string, Expression>? aliases = null, IReadOnlyDictionary<string, TypedParameter>? typed = null)
        => new Parser(source, parameters, aliases, typed).Parse();
    internal static void ValidateSize(Expression expression)
    {
        int nodes = 0;
        void Visit(Expression e, int depth)
        {
            Composition.Require(++nodes <= 128 && depth <= 32, "Expression exceeds 128 nodes or AST depth 32.");
            if (e is Unary u) Visit(u.Operand, depth + 1);
            if (e is Binary b) { Visit(b.Left, depth + 1); Visit(b.Right, depth + 1); }
            if (e is LogicalBinary l) { Visit(l.Left, depth + 1); Visit(l.Right, depth + 1); }
            if (e is TypedEquality t) { Visit(t.Left, depth + 1); Visit(t.Right, depth + 1); }
            if (e is Function f) foreach (var a in f.Arguments) Visit(a, depth + 1);
        }
        Visit(expression, 1);
    }

    private sealed class Parser
    {
        private readonly string source;
        private readonly HashSet<string> names;
        private readonly IReadOnlyDictionary<string, Expression>? aliases;
        private readonly IReadOnlyDictionary<string, TypedParameter>? typed;
        private int position, nodes;
        private string token = "";
        internal Parser(string source, IEnumerable<string> parameters, IReadOnlyDictionary<string, Expression>? aliases, IReadOnlyDictionary<string, TypedParameter>? typed)
        {
            Composition.Require(source.Length <= 1024, "Expression exceeds 1024 characters.");
            this.source = source; names = parameters.ToHashSet(StringComparer.Ordinal); this.aliases = aliases; this.typed = typed; Next();
        }
        private void Next()
        {
            while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
            if (position == source.Length) { token = ""; return; }
            int start = position; char c = source[position++];
            if (c == '\'')
            {
                while (position < source.Length && source[position] != '\'') position++;
                Composition.Require(position < source.Length, "Unterminated enum literal."); position++;
            }
            else if (position < source.Length && source[position] == c && c is '&' or '|') position++;
            else if (char.IsAsciiDigit(c) || c == '.')
                while (position < source.Length && (char.IsAsciiDigit(source[position]) || source[position] == '.')) position++;
            else if (char.IsAsciiLetter(c) || c is '_' or '$')
                while (position < source.Length && (char.IsAsciiLetterOrDigit(source[position]) || source[position] == '_')) position++;
            else if (position < source.Length && source[position] == '=' && c is '<' or '>' or '=' or '!') position++;
            token = source[start..position];
        }
        internal Expression Parse()
        {
            var result = Read(-2, 0); Composition.Require(token == "", $"Unexpected token '{token}'."); ValidateSize(result); return result;
        }
        private Expression Count(Expression value) { Composition.Require(++nodes <= 128, "Expression exceeds 128 nodes."); return value; }
        private static int Priority(string t) => t switch { "||" => -2, "&&" => -1, "==" or "!=" or ">" or "<" or ">=" or "<=" => 1, "+" or "-" => 2, "*" or "/" => 3, _ => -3 };
        private Expression Read(int minimum, int depth)
        {
            Composition.Require(depth < 32, "Expression nesting exceeds 32.");
            Expression left; var t = token; Next();
            if (t == "-") { var operand = Read(4, depth + 1); Numeric(operand); left = Count(new Unary(operand)); }
            else if (t == "(") { left = Read(-2, depth + 1); Expect(")"); }
            else if (decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n)) left = Count(new Literal(n));
            else if (t is "true" or "false" && !names.Contains(t) && !(aliases?.ContainsKey(t) ?? false)) left = Count(new BooleanLiteral(t == "true"));
            else if (t.StartsWith("'", StringComparison.Ordinal))
            { var text = t[1..^1]; Composition.Require(TypedParameters.Identifier(text), "Enum literal must be an identifier."); left = Count(new EnumLiteral(text)); }
            else if (token == "(")
            {
                Next(); var items = ImmutableArray.CreateBuilder<Expression>();
                if (token != ")") { items.Add(Read(-2, depth + 1)); while (token == ",") { Next(); items.Add(Read(-2, depth + 1)); } }
                Expect(")");
                int count = t switch { "abs" => 1, "min" or "max" => 2, "clamp" => 3, _ => -1 };
                Composition.Require(items.Count == count, $"Unknown function or incorrect argument count: '{t}'.");
                foreach (var item in items) Numeric(item);
                left = Count(new Function(t, items.ToImmutable()));
            }
            else
            {
                var name = t.TrimStart('$');
                if (aliases != null && aliases.TryGetValue(name, out var alias)) left = Count(alias);
                else if (typed != null && typed.TryGetValue(name, out var declaration)) left = Count(new TypedVariable(name, declaration));
                else { Composition.Require(names.Contains(name) || Builtins.Contains(name), $"Unknown expression variable '{name}'."); left = Count(new Variable(name)); }
            }
            while (Priority(token) >= minimum)
            {
                var op = token; int priority = Priority(op); Next(); var right = Read(priority + 1, depth + 1);
                if (op is "&&" or "||")
                {
                    Composition.Require(left.Kind == ValueKind.Boolean && right.Kind == ValueKind.Boolean, "Logical operands must be boolean.");
                    left = Count(new LogicalBinary(op, left, right));
                }
                else if (op is "==" or "!=" && left.Kind != ValueKind.Number)
                {
                    Composition.Require(left.Kind == right.Kind, "Equality requires matching types.");
                    if (left.Kind == ValueKind.Symbol)
                    {
                        var variable = left as TypedVariable ?? right as TypedVariable;
                        Composition.Require(variable != null, "Enum equality requires a declared parameter.");
                        foreach (var literal in new[] { left, right }.OfType<EnumLiteral>()) Composition.Require(variable.Declaration.Values.Contains(literal.Text, StringComparer.Ordinal), "Unknown enum literal.");
                        foreach (var other in new[] { left, right }.OfType<TypedVariable>()) Composition.Require(other.Declaration.Values.SequenceEqual(variable.Declaration.Values), "Enum domains must match.");
                    }
                    left = Count(new TypedEquality(op, left, right));
                }
                else { Numeric(left); Numeric(right); left = Count(new Binary(op, left, right, priority == 1 ? ValueKind.Boolean : ValueKind.Number)); }
            }
            return left;
        }
        private void Expect(string value) { Composition.Require(token == value, $"Expected '{value}', got '{token}'."); Next(); }
        private static void Numeric(Expression e) => Composition.Require(e.Kind == ValueKind.Number, "Numeric expression required.");
    }
}
