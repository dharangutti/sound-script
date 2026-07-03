using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class OrchestrationHelperTests
{
    [Fact]
    public void Apply_DoubleOctave_AddsUpperOctaveDoubles()
    {
        var notes = new[] { 60, 64, 67 };
        var settings = new OrchestrationSettings { DoubleOctave = true };
        var (orchestrated, adjusted) = ChordOrchestration.Apply(notes, settings);

        Assert.True(adjusted);
        Assert.Equal([60, 64, 67, 72, 76, 79], orchestrated);
    }

    [Fact]
    public void Apply_ReinforceBass_AddsLowerRoot()
    {
        var notes = new[] { 60, 64, 67 };
        var settings = new OrchestrationSettings { ReinforceBass = true };
        var (orchestrated, adjusted) = ChordOrchestration.Apply(notes, settings);

        Assert.True(adjusted);
        Assert.Equal(48, orchestrated[0]);
        Assert.Equal(4, orchestrated.Length);
    }

    [Fact]
    public void Apply_BrightenTop_AddsUpperExtension()
    {
        var notes = new[] { 60, 64, 67 };
        var settings = new OrchestrationSettings { BrightenTop = true };
        var (orchestrated, adjusted) = ChordOrchestration.Apply(notes, settings);

        Assert.True(adjusted);
        Assert.Equal(79, orchestrated[^1]);
        Assert.Equal(4, orchestrated.Length);
    }

    [Fact]
    public void Parse_OrchestrationStatements()
    {
        const string source = """
            track melody {
                double octave
                reinforce bass
                brighten top
                Cmaj q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);

        Assert.IsType<OrchestrationNode>(track.Body[0]);
        Assert.IsType<OrchestrationNode>(track.Body[1]);
        Assert.IsType<OrchestrationNode>(track.Body[2]);
    }

    [Fact]
    public void Interpret_AppliesOrchestrationBeforeHarmonicSpacing()
    {
        const string source = """
            track melody {
                double octave
                Cmaj q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes.Select(note => note.MidiNumber).OrderBy(number => number).ToArray();

        Assert.Equal(6, notes.Length);
        Assert.True(notes.Count(number => number >= 72) >= 3);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Orchestration applied"));
    }

    [Fact]
    public void Interpret_ReinforceBass_ExpandsChord()
    {
        const string source = """
            track melody {
                reinforce bass
                Cmaj q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes.Select(note => note.MidiNumber).OrderBy(number => number).ToArray();
        Assert.Contains(48, notes);
    }

    [Fact]
    public void Interpret_BrightenTop_ExpandsChord()
    {
        const string source = """
            track melody {
                brighten top
                Cmaj q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes.Select(note => note.MidiNumber).OrderBy(number => number).ToArray();
        Assert.True(notes[^1] > 67);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Orchestration applied"));
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
