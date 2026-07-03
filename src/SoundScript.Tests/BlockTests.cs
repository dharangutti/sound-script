using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class BlockTests
{
    [Fact]
    public void Interpret_ExpandsBasicBlock()
    {
        const string source = """
            block intro {
                C4 q
                E4 q
                G4 q
            }
            track melody {
                play intro
            }
            """;

        var interpreted = Interpret(source);
        var track = Assert.Single(interpreted.Tracks);
        Assert.Equal(3, track.Notes.Count);
        Assert.Equal([60, 64, 67], track.Notes.Select(n => n.MidiNumber).ToArray());
    }

    [Fact]
    public void Interpret_BlockSupportsDynamicsAndArticulations()
    {
        const string source = """
            block phrase {
                p C4 q
                f accent G4 q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var block = Assert.IsType<BlockNode>(program.Statements[0]);

        Assert.Equal(4, block.Body.Count);
        Assert.IsType<DynamicNode>(block.Body[0]);
        Assert.IsType<NoteNode>(block.Body[1]);
        Assert.IsType<DynamicNode>(block.Body[2]);
        var accented = Assert.IsType<NoteNode>(block.Body[3]);
        Assert.Equal(ArticulationType.Accent, accented.Notation.Articulation);
    }

    [Fact]
    public void Interpret_ExpandsNestedBlocks()
    {
        const string source = """
            block tail {
                G4 q
            }
            block intro {
                C4 q
                E4 q
                play tail
            }
            track melody {
                play intro
            }
            """;

        var interpreted = Interpret(source);
        var track = Assert.Single(interpreted.Tracks);
        Assert.Equal(3, track.Notes.Count);
        Assert.Equal([60, 64, 67], track.Notes.Select(n => n.MidiNumber).ToArray());
    }

    [Fact]
    public void Interpret_PreservesPhraseBoundariesBetweenBlockCalls()
    {
        const string source = """
            block phrasea {
                C6 q
            }
            track melody {
                play phrasea
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(84, notes[0].MidiNumber);
        Assert.Equal(72, notes[1].MidiNumber);
        Assert.Contains(interpreted.Warnings, w => w.Contains("Phrase smoothing applied"));
    }

    [Fact]
    public void Interpret_RejectsDirectRecursion()
    {
        const string source = """
            block looped {
                C4 q
                play looped
            }
            track melody {
                play looped
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => Interpret(source));
        Assert.Contains("Recursive block call", ex.Message);
    }

    [Fact]
    public void Interpret_RejectsIndirectRecursion()
    {
        const string source = """
            block a {
                play b
            }
            block b {
                play a
            }
            track melody {
                play a
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => Interpret(source));
        Assert.Contains("Recursive block call", ex.Message);
    }

    [Fact]
    public void Load_ImportsBlocksFromOtherFiles()
    {
        using var dir = new TempDirectory();
        dir.Write("lib.ss", """
            block intro {
                C4 q
                D4 q
            }
            """);
        dir.Write("main.ss", """
            import "lib.ss"
            track melody {
                play intro
            }
            """);

        var loaded = ProgramLoader.Load(dir.FilePath("main.ss"));
        var interpreted = Interpreter.Interpret(loaded.Program);
        var track = Assert.Single(interpreted.Tracks);

        Assert.Equal(2, track.Notes.Count);
        Assert.Equal([60, 62], track.Notes.Select(n => n.MidiNumber).ToArray());
    }

    [Fact]
    public void Parse_BlockStatement()
    {
        const string source = """
            block intro {
                C4 q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var block = Assert.IsType<BlockNode>(program.Statements[0]);
        Assert.Equal("intro", block.Name);
        Assert.Single(block.Body, s => s is NoteNode);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "soundscript-test-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Root);

        public void Write(string fileName, string content) =>
            File.WriteAllText(FilePath(fileName), content);

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
