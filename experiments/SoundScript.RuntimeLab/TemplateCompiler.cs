using System.Globalization;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Visual;

namespace SoundScript.RuntimeLab;

internal sealed record GainSlot(int Statement, int Body, int Parameter);
internal sealed record VisualSlot(string Name, string Property, int Parameter);
internal sealed record CompiledTemplate(ProgramNode Program, VisualTimeline Timeline,
    RuntimeParameter[] Parameters, GainSlot[] Gains, VisualSlot[] Visuals);

/// <summary>The only Lab component that can see source or invoke a parser.</summary>
internal static class TemplateCompiler
{
    private sealed record Reference(Token Owner, Token Directive, string Property, int Parameter);

    internal static CompiledTemplate Compile(string source, CompilationCounters counters)
    {
        ArgumentNullException.ThrowIfNull(source);
        counters.Tokenizations++;
        var tokens = new Tokenizer(source).Tokenize();
        var declarations = new List<(string Name, decimal Default)>();
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var position = 0;
        // Experimental declarations must precede production source. Comments and strings
        // have already been handled by the authoritative lexer; no textual replacement.
        while (Word(tokens[position], "param"))
        {
            if (position + 4 >= tokens.Count) throw Error(tokens[position], "Expected param name = decimal-default.");
            var start = tokens[position++];
            var name = tokens[position++];
            if (name.Type != TokenType.Identifier || !name.Value.All(char.IsAsciiLetterOrDigit)
                || !char.IsAsciiLetter(name.Value[0]) || IsContextual(name.Value))
                throw Error(name, "Parameter name must be an unreserved identifier.");
            if (!names.TryAdd(name.Value, declarations.Count))
                throw Error(name, "Duplicate parameter declaration.");
            if (tokens[position++].Type != TokenType.Assign)
                throw Error(start, "Expected param name = decimal-default.");
            var sign = 1m;
            if (tokens[position].Value is "+" or "-") sign = tokens[position++].Value == "-" ? -1m : 1m;
            var value = tokens[position++];
            if (value.Type != TokenType.Number || !decimal.TryParse(value.Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var number))
                throw Error(value, "A decimal literal default is required.");
            declarations.Add((name.Value, sign * number));
        }
        tokens.RemoveRange(0, position);

        var references = new List<Reference>();
        var depth = 0;
        Token owner = default;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (depth == 0 && (token.Type == TokenType.Track || Word(token, "visual"))) owner = token;
            if (token.Type == TokenType.LeftBrace) { depth++; continue; }
            if (token.Type == TokenType.RightBrace) { depth--; if (depth == 0) owner = default; continue; }
            if (token.Type != TokenType.Identifier || !names.TryGetValue(token.Value, out var parameter)) continue;
            // A property named x may also have a parameter named x: set x x.
            if (depth == 1 && Word(owner, "visual") && i > 0 && Word(tokens[i - 1], "set")) continue;
            string property;
            Token directive;
            if (depth == 1 && owner.Type == TokenType.Track && i > 0 && tokens[i - 1].Type == TokenType.Gain)
            {
                property = "gain";
                directive = tokens[i - 1];
            }
            else if (depth == 1 && Word(owner, "visual") && i > 1 && Word(tokens[i - 2], "set"))
            {
                property = tokens[i - 1].Value.ToLowerInvariant();
                directive = tokens[i - 2];
            }
            else throw Error(token, "Runtime references are limited to direct track gain and visual set values.");
            _ = Range(property);
            if (tokens[i + 1].Value is "+" or "-" or "*" or "/" or "(")
                throw Error(token, "Runtime expressions are not supported; use a parameter alone.");
            references.Add(new(owner, directive, property, parameter));
            tokens[i] = token with { Type = TokenType.Number,
                Value = declarations[parameter].Default.ToString(CultureInfo.InvariantCulture) };
        }

        var parameters = declarations.Select((declaration, index) =>
        {
            var targets = references.Where(r => r.Parameter == index).Select(r => Range(r.Property)).ToArray();
            if (targets.Length == 0) throw new ArgumentException($"Parameter '{declaration.Name}' has no approved binding.");
            var result = new RuntimeParameter(declaration.Name, declaration.Default,
                targets.Max(r => r.Min), targets.Min(r => r.Max));
            result.Validate(result.Default);
            return result;
        }).ToArray();

        counters.Parses++;
        var program = Parse(tokens, parameters.Length != 0);
        if (program.Statements.OfType<ImportNode>().Any())
            throw new NotSupportedException("RuntimeLab accepts in-memory source without imports.");
        if (references.Any(r => r.Property == "gain") && !program.Statements.OfType<PerformNode>().Any())
            throw new ArgumentException("Runtime gain requires the existing 'perform expressive' mode.");

        var gains = new List<GainSlot>();
        var visuals = new List<VisualSlot>();
        foreach (var reference in references)
        {
            var statement = program.Statements.FindIndex(n => At(n, reference.Owner));
            if (statement < 0) throw new ArgumentException("Runtime reference has no matching top-level target.");
            if (reference.Property == "gain")
            {
                var track = (TrackNode)program.Statements[statement];
                var body = track.Body.FindIndex(n => n is GainNode && At(n, reference.Directive));
                if (body < 0) throw new ArgumentException("Runtime gain has no matching direct track target.");
                gains.Add(new(statement, body, reference.Parameter));
            }
            else
            {
                var visual = (VisualNode)program.Statements[statement];
                if (program.Statements.OfType<VisualNode>().Count(v => v.Name == visual.Name) != 1)
                    throw new ArgumentException("Runtime-bound visuals require unique names.");
                var automation = visual.Automations.Single(a => a.Property.Equals(reference.Property, StringComparison.OrdinalIgnoreCase));
                if (automation.From != parameters[reference.Parameter].Default || automation.To != automation.From)
                    throw new ArgumentException("Runtime binding must target a constant set value.");
                visuals.Add(new(visual.Name, reference.Property, reference.Parameter));
            }
        }
        counters.TimelineCompilations++;
        return new(program, VisualInterpreter.Interpret(program), parameters, gains.ToArray(), visuals.ToArray());
    }

    private static bool At(AstNode node, Token token) =>
        SourceLocation.For(node) is { } location && location.Line == token.Line && location.Column == token.Column;
    private static ProgramNode Parse(List<Token> tokens, bool parameterized)
    {
        // Production gain parsing uses CurrentCulture, while visual decimals use
        // invariant culture. Lab parameter tokens have an invariant contract.
        // Keep ordinary source behavior intact and always restore the caller's context.
        if (!parameterized) return new SoundScript.Parser.Parser(tokens).Parse();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return new SoundScript.Parser.Parser(tokens).Parse();
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    private static bool Word(Token token, string word) => token.Type == TokenType.Identifier
        && token.Value.Equals(word, StringComparison.OrdinalIgnoreCase);
    private static bool IsContextual(string name) => name.ToLowerInvariant() is
        "param" or "let" or "marker" or "style" or "visual" or "set" or "animate" or "shape" or "fill"
        or "stroke" or "strokewidth" or "fontsize" or "text" or "use" or "at" or "wait" or "sync"
        or "audio" or "perform" or "expressive";
    private static ArgumentException Error(Token token, string message) =>
        new($"RuntimeLab {token.Line}:{token.Column}: {message}");
    private static (decimal Min, decimal Max) Range(string property) => property switch
    {
        "gain" or "opacity" => (0m, 1m),
        "x" => (-12800m, 12800m),
        "y" => (-7200m, 7200m),
        "width" => (8m, 1280m),
        "height" => (8m, 720m),
        "rotation" => (-360m, 360m),
        _ => throw new ArgumentException($"Unsupported runtime property '{property}'.")
    };
}
