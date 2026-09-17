using System.Security.Cryptography;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Wave;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public sealed class SoundScriptPackageApiTests
{
    [Fact]
    public void Compile_RenderWave_MatchesExistingDirectRenderer()
    {
        const string source = "tempo 120 track lead { C4 q E4 q G4 h }";
        var compilation = global::SoundScript.SoundScriptEngine.Compile(source);
        var facadeBytes = compilation.RenderWave();
        var program = Parse(source);

        Assert.Equal(WaveRenderer.RenderToBytes(program), facadeBytes);
        Assert.Equal(Hash(WaveRenderer.RenderToBytes(program)), Hash(facadeBytes));
    }

    [Fact]
    public void Compile_RenderMidi_MatchesExistingInterpreterAndWriter()
    {
        const string source = "tempo 96 track lead { instrument piano C4 q G4 q C5 h }";
        var compilation = global::SoundScript.SoundScriptEngine.Compile(source);
        using var expected = new MemoryStream();
        MidiGenerator.Write(Interpreter.Interpret(Parse(source)), expected);

        Assert.Equal(expected.ToArray(), compilation.RenderMidi());
    }

    [Fact]
    public void CompileFile_ResolvesImportsAndRelativeSamplePaths()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("library.ss", "track melody { C4 q E4 q }");
        directory.Write("main.ssw", "import \"library.ss\"\nsample \"assets/tone.wav\" gain=0.25 at=0");
        Directory.CreateDirectory(Path.Combine(directory.Root, "assets"));
        var tone = global::SoundScript.SoundScriptEngine.Compile("tempo 120 track tone { G5 q }").RenderWave();
        File.WriteAllBytes(Path.Combine(directory.Root, "assets", "tone.wav"), tone);

        var compilation = global::SoundScript.SoundScriptEngine.CompileFile(Path.Combine(directory.Root, "main.ssw"));
        var rendered = compilation.RenderWave();

        Assert.Empty(compilation.Warnings);
        Assert.True(rendered.Length > 44);
        Assert.Contains(rendered.Skip(44), sample => sample != 0);
    }

    [Fact]
    public void CompileFile_PreservesRelativeImportWarnings()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("one.ss", "track melody { C4 q }");
        directory.Write("two.ss", "track melody { D4 q }");
        directory.Write("main.ss", "import \"one.ss\"\nimport \"two.ss\"");

        var compilation = global::SoundScript.SoundScriptEngine.CompileFile(Path.Combine(directory.Root, "main.ss"));

        Assert.Contains(compilation.Warnings, warning => warning.Contains("Duplicate block name 'melody'", StringComparison.Ordinal));
    }

    [Fact]
    public void CompileFile_RenderWave_IsByteStableAcrossRepeatedCalls()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Write("stable.ssw", "tempo 110 track pad { humanize timing=0.02 velocity=0.05 seed=17 C4 q E4 q G4 h }");
        var compilation = global::SoundScript.SoundScriptEngine.CompileFile(path);

        var first = compilation.RenderWave();
        var second = compilation.RenderWave();

        Assert.Equal(first, second);
        Assert.Equal(Hash(first), Hash(second));
    }

    [Fact]
    public void RenderMidi_ReportsUnsupportedUnpitchedHit()
    {
        var compilation = global::SoundScript.SoundScriptEngine.Compile("tempo 120 track drums { hit kick :0.25 }");

        var exception = Assert.Throws<NotSupportedException>(() => compilation.RenderMidi());

        Assert.Contains("unpitched", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wave backend", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_RejectsImportsWithGuidanceToUseCompileFile()
    {
        var exception = Assert.Throws<NotSupportedException>(() => global::SoundScript.SoundScriptEngine.Compile("import \"library.ss\""));

        Assert.Contains("CompileFile", exception.Message, StringComparison.Ordinal);
    }

    private static ProgramNode Parse(string source) => new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-facade-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Root);

        public string Write(string fileName, string content)
        {
            var path = Path.Combine(Root, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
