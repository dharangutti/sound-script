using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Midi;
using SoundScript.Parser;
using PlaygroundPage = SoundScript.Playground.Pages.Playground;
using SoundScript.Timbre;
using SoundScript.Visual;
using SoundScript.Wave;
using Xunit;

namespace SoundScript.Tests;

public class ExampleCompilationTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    private static readonly JsonSerializerOptions SnapshotOptions = new() { Converters = { new AstConverter() } };

    [Fact]
    public void ReviewedInventory_RemainsDiscoverable()
    {
        var keys = Files().Select(row => (string)row[0])
            .Concat(Presets().Select(row => "playground/" + (string)row[0])).Order(StringComparer.Ordinal);
        Assert.Equal(ReadBaselines().Keys.Order(StringComparer.Ordinal), keys);
    }

    // Discover new files automatically, including downloadable website demos and import libraries.
    public static IEnumerable<object[]> Files() => new[] { "examples", "docs/assets/demos" }
        .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(Root, directory), "*", SearchOption.AllDirectories))
        .Where(path => Path.GetExtension(path) is ".ss" or ".ssw" or ".ssv" or ".ssc")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(path => new object[] { Path.GetRelativePath(Root, path).Replace('\\', '/') });

    [Theory]
    [MemberData(nameof(Files))]
    public void RepositoryExample_CompilesAndPreservesBaseline(string relativePath)
    {
        var path = Path.Combine(Root, relativePath);
        var source = File.ReadAllText(path);
        if (Path.GetExtension(path) == ".ssc")
        {
            CompileCss(relativePath, source);
            return;
        }

        // ProgramLoader resolves imports relative to the source, just like the CLI.
        var program = ProgramLoader.Load(path).Program;
        var rail = Path.GetExtension(path) == ".ssv" ? "visual"
            : Path.GetExtension(path) == ".ssw" || Path.GetFileName(path) is "speech-only-wave.ss" or "full-song-wave.ss"
                ? "wave" : "midi";
        Compile(relativePath, program, rail, Path.GetDirectoryName(path)!);
    }

    public static IEnumerable<object[]> Presets()
    {
        var source = File.ReadAllText(Path.Combine(Root, "src/SoundScript.Playground/Pages/Playground.razor.cs"));
        var matches = Regex.Matches(source,
            "private void (Load\\w+Example)\\(\\)\\s*\\{\\s*(WaveScriptText|ScriptText)\\s*=\\s*\"\"\"\\r?\\n(.*?)\"\"\";",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        foreach (Match match in matches)
            yield return new object[] { match.Groups[1].Value, match.Groups[3].Value,
                match.Groups[2].Value == "WaveScriptText" ? "wave" : "midi" };

        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        yield return new object[] { "DefaultScript", (string)typeof(PlaygroundPage).GetField("DefaultScript", flags)!.GetRawConstantValue()!, "midi" };
        var safe = ((string Ssw, string Css)[])typeof(PlaygroundPage).GetField("SafeExamples", flags)!.GetValue(null)!;
        for (var i = 0; i < safe.Length; i++)
        {
            yield return new object[] { $"Studio-{i + 1}", safe[i].Ssw, "wave" };
            yield return new object[] { $"Studio-{i + 1}-css", safe[i].Css, "css" };
        }
        yield return new object[] { "SyntaxExample", (string)typeof(PlaygroundPage).GetField("SyntaxExample", flags)!.GetRawConstantValue()!, "css" };
        yield return new object[] { "DefaultStylesheet", OfflineRenderer.DefaultStylesheet, "css" };
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void PlaygroundExample_CompilesAndPreservesBaseline(string name, string source, string rail)
    {
        var key = "playground/" + name;
        if (rail == "css") CompileCss(key, source);
        else Compile(key, new Parser.Parser(new Tokenizer(source).Tokenize()).Parse(), rail, Path.Combine(Root, "examples"));
    }

    private static void Compile(string key, ProgramNode program, string rail, string directory)
    {
        Assert.NotEmpty(program.Statements);
        // Snapshot before interpretation because the interpreter records derived measure data on some AST nodes.
        AssertBaseline(key, JsonSerializer.SerializeToUtf8Bytes(program, SnapshotOptions));
        switch (rail)
        {
            case "visual":
                var timeline = VisualInterpreter.Interpret(program);
                Assert.NotEmpty(TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline)).Samples);
                Assert.NotEmpty(TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration));
                break;
            case "wave":
                Assert.NotEmpty(WaveRenderer.RenderToBytes(program, new WaveRenderOptions { ScriptDirectory = directory }));
                break;
            case "midi":
                var interpreted = Interpreter.Interpret(program);
                var output = Path.GetTempFileName();
                try
                {
                    MidiGenerator.Write(interpreted, output);
                    Assert.True(new FileInfo(output).Length > 0);
                }
                finally { File.Delete(output); }
                break;
        }
    }

    private static void CompileCss(string key, string source)
    {
        var profiles = SoundCSSParser.Parse(source);
        var plans = SoundCSSParser.ParseTransformPlans(source);
        Assert.True(profiles.Count > 0 || plans.Count > 0);
        AssertBaseline(key, JsonSerializer.SerializeToUtf8Bytes(new { profiles, plans }));
    }

    private static void AssertBaseline(string key, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var baselines = ReadBaselines();
        Assert.True(baselines.TryGetValue(key, out var expected), $"Missing reviewed baseline for {key}");
        Assert.Equal(expected, hash);
    }

    private static Dictionary<string, string> ReadBaselines() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Golden/example-programs.json")))!;

    // Include concrete node properties at every nesting level; AstNode alone serializes as {}.
    private sealed class AstConverter : JsonConverter<AstNode>
    {
        public override AstNode Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, AstNode value, JsonSerializerOptions options)
        {
            // Bar line numbers are diagnostic locations, not musical timing or output.
            if (value is BarNode) value = new BarNode(0);
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
