using System.Globalization;
using SoundScript.Core;
using SoundScript.Core.Ast;

namespace SoundScript.Parser;

public sealed partial class Parser
{
    private sealed record Constant(Token Value, TimeSpan? Time = null);
    private readonly Dictionary<string, Constant> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, Token>> _styles = new(StringComparer.Ordinal);

    // Declarations are file-local and disappear before the AST is returned.
    // References resolve only in value positions, never in names or strings.
    private void ParseConstantDeclaration(bool marker)
    {
        var name = Expect(TokenType.Identifier, "constant name (an unreserved identifier)");
        if (!name.Value.All(char.IsLetterOrDigit) || !char.IsLetter(name.Value[0]))
            throw Invalid(name, "Constant names must start with a letter and contain letters or digits.");
        if (_constants.ContainsKey(name.Value)) throw Invalid(name, $"Duplicate constant '{name.Value}'.");
        Expect(TokenType.Assign, "=");
        var value = Peek();
        Constant constant;
        if (marker)
            constant = new Constant(value, ParseSeconds("marker", true));
        else if (Check(TokenType.StringLiteral))
            constant = new Constant(Advance());
        else if (Check(TokenType.Identifier) && _constants.TryGetValue(value.Value, out var alias)
                 && (alias.Time is not null || alias.Value.Type is TokenType.Number or TokenType.StringLiteral))
        {
            Advance();
            constant = alias;
        }
        else if (Check(TokenType.Number) && IsTimeUnit(PeekNext().Value))
            constant = new Constant(value, ParseSeconds("time constant", true));
        else
            constant = new Constant(value with { Type = TokenType.Number,
                Value = ParseNumericExpression().ToString(CultureInfo.InvariantCulture) });
        _constants.Add(name.Value, constant);
    }

    private static bool IsTimeUnit(string unit) => unit.ToLowerInvariant() is
        "s" or "sec" or "secs" or "second" or "seconds" or "ms" or "millisecond" or "milliseconds";

    // Arithmetic is deliberately limited to declarations and set values. In
    // particular, '/' in time signatures and note durations keeps its meaning.
    private decimal ParseNumericExpression(int precedence = 0, int depth = 0)
    {
        var start = Peek();
        if (depth > 64) throw Invalid(start, "Expression nesting exceeds 64 levels.");
        try
        {
            decimal value;
            if (start.Value is "+" or "-")
            {
                Advance();
                value = ParseNumericExpression(3, depth + 1) * (start.Value == "-" ? -1 : 1);
            }
            else if (start.Value == "(")
            {
                Advance();
                value = ParseNumericExpression(0, depth + 1);
                if (Peek().Value != ")") throw Invalid(Peek(), "Expected ) in expression.");
                Advance();
            }
            else
            {
                if (Check(TokenType.Identifier) && !_constants.ContainsKey(start.Value))
                    throw Invalid(start, $"Unknown numeric constant '{start.Value}'. Declare it before use.");
                var token = Expect(TokenType.Number, "number or numeric constant");
                if (!decimal.TryParse(token.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out value))
                    throw Invalid(start, "Invalid numeric constant.");
            }
            while (true)
            {
                var op = Peek();
                var nextPrecedence = op.Value switch { "+" or "-" => 1, "*" or "/" => 2, _ => 0 };
                if (nextPrecedence <= precedence) return value;
                Advance();
                var right = ParseNumericExpression(nextPrecedence, depth + 1);
                value = op.Value switch { "+" => value + right, "-" => value - right,
                    "*" => value * right, _ => value / right };
            }
        }
        catch (Exception ex) when (ex is OverflowException or DivideByZeroException)
        {
            throw Invalid(start, "Numeric expression overflows or divides by zero.");
        }
    }

    private void ParseStyleDeclaration()
    {
        var name = Expect(TokenType.StringLiteral, "quoted style name");
        if (_styles.ContainsKey(name.Value)) throw Invalid(name, $"Duplicate style '{name.Value}'.");
        Expect(TokenType.LeftBrace, "{");
        var values = new Dictionary<string, Token>(StringComparer.OrdinalIgnoreCase);
        while (!Check(TokenType.RightBrace) && !Check(TokenType.EndOfFile))
        {
            var property = Advance();
            var key = property.Value.ToLowerInvariant();
            if (key is not ("fill" or "stroke" or "strokewidth" or "fontsize"))
                throw Invalid(property, "Styles support fill, stroke, strokeWidth, and fontSize; declare shape and text on the visual.");
            var value = Expect(key is "fill" or "stroke" ? TokenType.StringLiteral : TokenType.Number, key);
            if (!values.TryAdd(key, value)) throw Invalid(property, $"Duplicate style property '{key}'.");
            // Validate even unused styles. Cross-property shape rules remain on the visual.
            var probe = ApplyStyleValue(new VisualPresentation { Shape = key == "fontsize" ? "text" : "rectangle", Text = key == "fontsize" ? "" : null }, key, value);
            try { probe.Validate(); } catch (ArgumentException ex) { throw Invalid(value, ex.Message); }
        }
        Expect(TokenType.RightBrace, "}");
        _styles.Add(name.Value, values);
    }

    private static VisualPresentation ApplyStyleValue(VisualPresentation presentation, string key, Token value) => key switch
    {
        "fill" => presentation with { Fill = value.Value.ToLowerInvariant() },
        "stroke" => presentation with { Stroke = value.Value.ToLowerInvariant() },
        "strokewidth" => presentation with { StrokeWidth = ParseDecimal(value, "stroke width") },
        _ => presentation with { FontSize = ParseDecimal(value, "font size") }
    };
}
