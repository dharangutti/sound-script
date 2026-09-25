using System.Globalization;
using SoundScript.Core;
using SoundScript.Core.Ast;

namespace SoundScript.Parser;

/// <summary>An approved decimal parameter with the intersection of its target ranges.</summary>
public sealed record ParsedRuntimeParameter(string Name, decimal Default, decimal Minimum, decimal Maximum);
/// <summary>A direct track gain address, independent of numeric default values.</summary>
public sealed record RuntimeGainTarget(int Statement, int Body, string Parameter);
/// <summary>A constant visual-property binding.</summary>
public sealed record RuntimeVisualTarget(string Name, string Property, string Parameter);
/// <summary>Owned parse output for a runtime host; renderers continue to receive an ordinary AST.</summary>
public sealed record RuntimeParseResult(ProgramNode Program, IReadOnlyList<ParsedRuntimeParameter> Parameters,
    IReadOnlyList<RuntimeGainTarget> Gains, IReadOnlyList<RuntimeVisualTarget> Visuals);

public sealed partial class Parser
{
    private bool _runtimeMode;
    private sealed class Declaration(Token token, decimal initial)
    {
        public Token Token { get; } = token;
        public decimal Default { get; } = initial;
        public decimal Minimum { get; set; } = decimal.MinValue;
        public decimal Maximum { get; set; } = decimal.MaxValue;
        public int Uses { get; set; }
    }
    private readonly Dictionary<string, Declaration> _runtimeDeclarations = new(StringComparer.Ordinal);
    private readonly List<(GainNode Node, string Parameter)> _runtimeGains = [];
    private readonly List<(VisualAutomationNode Node, string Parameter)> _runtimeVisuals = [];

    /// <summary>Parses the opt-in runtime dialect once, preserving normal Parse semantics.</summary>
    /// <remarks>Uses invariant numbers and restores the caller's culture, even after failure.
    /// Runtime parameters support direct track gain and constant visual values; timing and topology are fixed.</remarks>
    public RuntimeParseResult ParseRuntime()
    {
        if (_position != 0 || _runtimeMode) throw new InvalidOperationException("Use a fresh parser for runtime compilation.");
        _runtimeMode = true;
        var culture = CultureInfo.CurrentCulture;
        ProgramNode program;
        try { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; program = Parse(); }
        finally { CultureInfo.CurrentCulture = culture; }
        if (program.Statements.OfType<ImportNode>().Any())
            throw new NotSupportedException("Runtime compilation does not resolve imports. Use CompileFile for static imported programs.");
        if (_runtimeGains.Count != 0 && !program.Statements.OfType<PerformNode>().Any())
            throw new InvalidOperationException("Runtime gain requires 'perform expressive'.");

        var gains = new List<RuntimeGainTarget>();
        var visuals = new List<RuntimeVisualTarget>();
        for (var i = 0; i < program.Statements.Count; i++)
        {
            if (program.Statements[i] is TrackNode track)
                for (var j = 0; j < track.Body.Count; j++)
                    foreach (var binding in _runtimeGains.Where(b => ReferenceEquals(b.Node, track.Body[j])))
                        gains.Add(new(i, j, binding.Parameter));
            if (program.Statements[i] is VisualNode visual)
                foreach (var binding in _runtimeVisuals.Where(b => visual.Automations.Any(a => ReferenceEquals(a, b.Node))))
                {
                    if (program.Statements.OfType<VisualNode>().Count(v => v.Name == visual.Name) != 1)
                        throw new InvalidOperationException("Runtime-bound visuals require unique names.");
                    visuals.Add(new(visual.Name, binding.Node.Property.ToLowerInvariant(), binding.Parameter));
                }
        }
        if (gains.Count != _runtimeGains.Count)
            throw new InvalidOperationException("Runtime gain is supported only directly inside a track, not inside loops, phrases or reusable blocks.");
        var parameters = _runtimeDeclarations.Select(pair =>
        {
            var d = pair.Value;
            if (d.Uses == 0) throw Invalid(d.Token, $"Runtime parameter '{pair.Key}' has no approved binding.");
            if (d.Minimum > d.Maximum || d.Default < d.Minimum || d.Default > d.Maximum)
                throw Invalid(d.Token, $"Runtime default for '{pair.Key}' must satisfy all target ranges ({d.Minimum} through {d.Maximum}).");
            return new ParsedRuntimeParameter(pair.Key, d.Default, d.Minimum, d.Maximum);
        }).ToArray();
        return new(program, Array.AsReadOnly(parameters), gains.AsReadOnly(), visuals.AsReadOnly());
    }

    private void ParseRuntimeDeclaration()
    {
        var name = Expect(TokenType.Identifier, "runtime parameter name");
        if (name.Value.Length == 0 || !char.IsAsciiLetter(name.Value[0]) || !name.Value.All(char.IsAsciiLetterOrDigit))
            throw Invalid(name, "Runtime names must begin with an ASCII letter and contain letters or digits.");
        if (_constants.ContainsKey(name.Value) || _runtimeDeclarations.ContainsKey(name.Value))
            throw Invalid(name, $"Duplicate parameter or constant '{name.Value}'.");
        Expect(TokenType.Assign, "=");
        var sign = 1m;
        if (Peek().Value is "+" or "-") sign = Advance().Value == "-" ? -1m : 1m;
        var value = Peek();
        if (value.Type != TokenType.Number || !decimal.TryParse(value.Value,
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var initial))
            throw Invalid(value, "Runtime defaults must be decimal literals.");
        Advance();
        _runtimeDeclarations.Add(name.Value, new(name, sign * initial));
    }

    private bool TryRuntimeValue(string property, out string parameter, out decimal initial)
    {
        parameter = ""; initial = 0;
        if (!_runtimeMode || !Check(TokenType.Identifier) || !_runtimeDeclarations.TryGetValue(Peek().Value, out var d)) return false;
        var token = Advance();
        parameter = token.Value; initial = d.Default;
        if (Peek().Value is "+" or "-" or "*" or "/" or "(")
            throw Invalid(token, "A runtime reference must stand alone; runtime expressions are not supported.");
        var range = property switch
        {
            "gain" or "opacity" => (0m, 1m),
            "x" => (-12800m, 12800m),
            "y" => (-7200m, 7200m),
            "width" => (8m, 1280m),
            "height" => (8m, 720m),
            "rotation" => (-360m, 360m),
            _ => throw Invalid(token, $"Property '{property}' is not runtime-bindable.")
        };
        d.Minimum = Math.Max(d.Minimum, range.Item1);
        d.Maximum = Math.Min(d.Maximum, range.Item2);
        d.Uses++;
        return true;
    }
}
