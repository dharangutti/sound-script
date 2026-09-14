using Microsoft.JSInterop;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundScript.Playground.Pages;

public partial class Playground
{
    private DotNetObjectReference<Playground>? _authoringReference;

    private sealed class AuthoringAstConverter : JsonConverter<AstNode>
    {
        public override AstNode Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    [JSInvokable]
    public string RenameConstant(string source, string oldName, string newName)
    {
        var nameTokens = new Tokenizer(newName).Tokenize();
        if (nameTokens.Count != 2 || nameTokens[0].Type != TokenType.Identifier || !newName.All(char.IsLetterOrDigit))
            throw new InvalidOperationException("Choose an unreserved identifier containing letters and digits.");
        var tokens = new Tokenizer(source).Tokenize();
        if (!tokens.Select((t, i) => (t, i)).Any(p => p.t.Value == oldName && p.i > 0 && tokens[p.i - 1].Value is "let" or "marker"))
            throw new InvalidOperationException("Safe rename currently supports let constants and markers declared in this file.");
        if (tokens.Any(t => t.Value == newName)) throw new InvalidOperationException("The new name is already present in this file.");
        var options = new JsonSerializerOptions { Converters = { new AuthoringAstConverter() } };
        var before = JsonSerializer.Serialize(new Parser.Parser(tokens).Parse(), options);
        var starts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++) if (source[i] == '\n') starts.Add(i + 1);
        var result = source;
        foreach (var token in tokens.Where(t => t.Type == TokenType.Identifier && t.Value == oldName).Reverse())
        {
            var offset = starts[token.Line - 1] + token.Column - 1;
            result = result[..offset] + newName + result[(offset + oldName.Length)..];
        }
        var after = JsonSerializer.Serialize(new Parser.Parser(new Tokenizer(result).Tokenize()).Parse(), options);
        if (before != after) throw new InvalidOperationException("Rename is ambiguous: the identifier also affects runtime names. Source was left unchanged.");
        return result;
    }

    [JSInvokable]
    public AuthoringReport ValidateAuthoring(string id, string source)
    {
        var wave = id is "ssw-editor" or "visual-timeline-source";
        try { wave |= ContainsWaveOnlyDirectives(new Parser.Parser(new Tokenizer(source).Tokenize()).Parse().Statements); }
        catch { /* Analyze supplies the source diagnostic. */ }
        return AuthoringAnalysis.Analyze(source, wave);
    }

    [JSInvokable]
    public object PrepareMusicPreview(string source, string[] muted, string[] solo)
    {
        var ast = new Parser.Parser(new Tokenizer(source).Tokenize()).Parse();
        if (ContainsWaveOnlyDirectives(ast.Statements))
            throw new InvalidOperationException("Range/mute/solo audition uses MIDI notes. Use the existing Wave player for this program.");
        var program = Interpreter.Interpret(ast);
        program.Tracks.RemoveAll(t => muted.Contains(t.Name) || (solo.Length > 0 && !solo.Contains(t.Name)));
        using var stream = new MemoryStream();
        MidiGenerator.Write(program, stream);
        var endBeat = program.Tracks.SelectMany(t => t.Notes).Select(n => n.StartBeat + n.DurationBeats).DefaultIfEmpty().Max();
        // Bound the display, not the score. Dense/very long scores remain valid.
        var stride = Math.Max(1, (int)Math.Ceiling(endBeat / 2000));
        var beats = Enumerable.Range(0, Math.Min(2001, (int)Math.Min(2000, Math.Ceiling(endBeat / stride)) + 1))
            .Select(i => new { beat = i * stride, seconds = program.TempoMap.BeatsToMilliseconds(0, i * stride) / 1000 }).ToArray();
        return new { midi = Convert.ToBase64String(stream.ToArray()), beats,
            beatsPerBar = (program.TimeSignatureNumerator ?? 4) * 4d / (program.TimeSignatureDenominator ?? 4),
            duration = program.TempoMap.BeatsToMilliseconds(0, endBeat) / 1000 };
    }

    private async Task InitializeAuthoringAsync()
    {
        _authoringReference = DotNetObjectReference.Create(this);
        foreach (var id in new[] { MidiEditorId, SswEditorId })
            await Js.InvokeVoidAsync("playgroundEditor.enableAuthoring", id, _authoringReference);
        await Js.InvokeVoidAsync("playgroundEditor.attachVisual", "visual-timeline-source", _authoringReference);
    }
}
