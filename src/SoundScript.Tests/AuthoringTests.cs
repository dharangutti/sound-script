using System.Text.Json;
using SoundScript.Core;
using SoundScript.Parser;
using SoundScript.Visual;
using SoundScript.Media;
using Xunit;

namespace SoundScript.Tests;

public class AuthoringTests
{
    private sealed class AstConverter : System.Text.Json.Serialization.JsonConverter<SoundScript.Core.Ast.AstNode>
    {
        public override SoundScript.Core.Ast.AstNode Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, SoundScript.Core.Ast.AstNode value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
    private static string Snapshot(SoundScript.Core.Ast.ProgramNode program) => JsonSerializer.Serialize(program,
        new JsonSerializerOptions { Converters = { new AstConverter() } });
    private static SoundScript.Core.Ast.ProgramNode Parse(string source) => new Parser.Parser(new Tokenizer(source).Tokenize()).Parse();

    [Fact]
    public void Comments_PreserveStringsNotationAndLocations()
    {
        var tokens = new Tokenizer("/* first\nsecond */\ntrack let { // named let\nC#4 q }\nimport \"https://host/a/*b*/.ss\"").Tokenize();
        var note = Assert.Single(tokens, t => t.Type == TokenType.Note);
        Assert.Equal((4, 1, "C#4"), (note.Line, note.Column, note.Value));
        Assert.Equal("https://host/a/*b*/.ss", Assert.Single(tokens, t => t.Type == TokenType.StringLiteral).Value);
        Parse("time 4/4 /* gap */ track style { C4 q // note\nD4 q }");
    }

    [Fact]
    public void Conveniences_LowerToIdenticalAstTimelineAndPcm()
    {
        const string expanded = "tempo 120 visual \"card\" for 1s at 0s { shape rectangle fill \"#2563eb\" strokeWidth 2 set x 640 set y 360 } track let { C4 q }";
        const string authored = """
            // Constants do not replace names or string contents.
            let speed = 120
            let primary = "#2563eb"
            let centerX = 600 + 2 * (30 - 10)
            let lifetime = 1000ms
            marker intro = 0s
            style "panel" { fill primary strokeWidth 2 }
            tempo speed
            visual "card" for lifetime at intro {
                shape rectangle
                use "panel"
                set x centerX
                set y 720 / 2
            }
            track let { C4 q }
            """;
        var a = Parse(expanded);
        var b = Parse(authored);
        Assert.Equal(Snapshot(a), Snapshot(b));
        var timelineA = VisualInterpreter.Interpret(a);
        var timelineB = VisualInterpreter.Interpret(b);
        foreach (var seconds in new[] { 0, 0.25, 0.999, 1 })
            Assert.Equal(JsonSerializer.Serialize(timelineA.StateAt(TimeSpan.FromSeconds(seconds))),
                JsonSerializer.Serialize(timelineB.StateAt(TimeSpan.FromSeconds(seconds))));
        Assert.Equal(TemporalAudioRenderer.RenderToWavBytes(a, timelineA.Duration),
            TemporalAudioRenderer.RenderToWavBytes(b, timelineB.Duration));
    }

    [Theory]
    [InlineData("let x = 1 let x = 2", "Duplicate constant")]
    [InlineData("let x = missing", "Unknown numeric constant")]
    [InlineData("let x = 1 / 0", "divides by zero")]
    [InlineData("let x = (1 + 2", "Expected )")]
    [InlineData("let x = \"red\" tempo x", "wrong type")]
    [InlineData("marker start = 4", "seconds unit")]
    [InlineData("let x = 1 visual \"a\" for x", "requires a time constant")]
    [InlineData("/* missing", "line 1, column 1")]
    [InlineData("style \"a\" { fill \"red\" }", "Colors must")]
    [InlineData("style \"a\" { shape circle }", "Styles support")]
    [InlineData("visual \"a\" for 1s { use \"missing\" }", "Unknown style")]
    [InlineData("style \"a\" {} style \"a\" {}", "Duplicate style")]
    public void InvalidConstructsHaveSourceDiagnostics(string source, string message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(source));
        Assert.Contains(message, ex.Message);
        Assert.Contains("line", ex.Message);
        Assert.Contains("column", ex.Message);
    }

    [Theory]
    [InlineData("use \"panel\" fill \"#ffffff\"")]
    [InlineData("fill \"#ffffff\" use \"panel\"")]
    public void LocalStyleOverridesAreOrderIndependent(string body)
    {
        var program = Parse("style \"panel\" { fill \"#000000\" } visual \"a\" for 1s { shape rectangle " + body + " }");
        Assert.Equal("#ffffff", VisualInterpreter.Interpret(program).Visuals[0].Presentation!.Fill);
    }

    [Fact]
    public void EveryExampleParsesWithAndWithoutComments()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples"));
        foreach (var path in Directory.GetFiles(root).Where(p => Path.GetExtension(p) is ".ss" or ".ssw" or ".ssv"))
        {
            var source = File.ReadAllText(path);
            var plain = Parse(source);
            var commented = Parse("/* compatibility */\n" + source + "\n// end");
            var commentedProgram = Parse("/* compatibility */\n" + source + "\n// end");
            Assert.Equal(plain.Statements.Count, commentedProgram.Statements.Count);
            Assert.Equal(plain.Statements.Select(s => s.GetType()).ToArray(),
                commentedProgram.Statements.Select(s => s.GetType()).ToArray());
        }
    }
}
