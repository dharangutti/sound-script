using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace SoundScript.Labs.Syntax;

// The AST retains spelling/locations. Only the compiler knows physical units.
public sealed record SourceToken(string Text, int Line, int Column);
public sealed record ExperimentAst(string Name, string Waveform,
    ImmutableDictionary<string, SourceToken> Parameters,
    ImmutableArray<SourceToken> Analyses, ImmutableArray<SourceToken> Exports);

public sealed class LabsException(string message) : Exception(message);

public static class LabsParser
{
    public const int MaxSourceLength = 65_536;

    public static ExperimentAst Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > MaxSourceLength) throw new LabsException("Source exceeds 65536 characters.");
        return new Reader(source).Parse();
    }

    private sealed class Reader
    {
        private readonly List<SourceToken> tokens = [];
        private int position;
        private SourceToken Current => tokens[position];

        public Reader(string source)
        {
            int line = 1, column = 1;
            // Every character is consumed; unsupported punctuation cannot disappear.
            foreach (Match match in Regex.Matches(source, @"//[^\r\n]*|\#[^\r\n]*|\s+|[{};]|[^\s{};]+"))
            {
                var value = match.Value;
                if (!char.IsWhiteSpace(value[0]) && !value.StartsWith('#') && !value.StartsWith("//"))
                    tokens.Add(new(value, line, column));
                foreach (char c in value) { if (c == '\n') { line++; column = 1; } else column++; }
            }
            tokens.Add(new("<eof>", line, column));
        }

        private SourceToken Take()
        {
            var token = Current;
            if (position == tokens.Count - 1) throw Error("Unexpected end of source.");
            position++;
            return token;
        }

        private bool Match(string text)
        {
            if (Current.Text != text) return false;
            position++;
            return true;
        }

        private void Expect(string text)
        {
            if (!Match(text)) throw Error($"Expected '{text}', got '{Current.Text}'.");
        }

        private LabsException Error(string message) => new($"Line {Current.Line}, column {Current.Column}: {message}");
        private void Separators() { while (Match(";")) { } }

        public ExperimentAst Parse()
        {
            string kind = Take().Text;
            if (kind is not ("signal" or "experiment")) throw Error("Expected signal or experiment.");
            string name = Take().Text;
            if (!Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]*\z")) throw Error("Invalid definition name.");
            Expect("{");
            string waveform;
            ImmutableDictionary<string, SourceToken> parameters;
            var analyses = ImmutableArray.CreateBuilder<SourceToken>();
            var exports = ImmutableArray.CreateBuilder<SourceToken>();
            if (kind == "signal")
            {
                parameters = Parameters();
                if (!parameters.TryGetValue("waveform", out var wave)) throw Error("Missing waveform.");
                waveform = wave.Text;
                parameters = parameters.Remove("waveform");
                foreach (var format in new[] { "json", "wav", "pcm", "float32" }) exports.Add(new(format, 1, 1));
            }
            else
            {
                Expect("generate");
                waveform = Take().Text;
                Expect("{");
                parameters = Parameters();
                Separators();
                if (Match("analyze"))
                {
                    Expect("{");
                    Separators();
                    while (!Match("}")) { analyses.Add(Take()); Separators(); }
                }
                Separators();
                while (Match("export")) { exports.Add(Take()); Separators(); }
                Expect("}");
                if (exports.Count == 0) exports.Add(new("json", 1, 1));
            }
            Separators();
            if (position != tokens.Count - 1) throw Error("Only one definition per file is supported.");
            return new(name, waveform, parameters, analyses.ToImmutable(), exports.ToImmutable());
        }

        private ImmutableDictionary<string, SourceToken> Parameters()
        {
            var values = ImmutableDictionary.CreateBuilder<string, SourceToken>(StringComparer.Ordinal);
            Separators();
            while (!Match("}"))
            {
                var key = Take();
                if (values.ContainsKey(key.Text)) throw Error($"Duplicate parameter '{key.Text}'.");
                values.Add(key.Text, Take());
                Separators();
            }
            return values.ToImmutable();
        }
    }
}
