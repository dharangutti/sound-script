using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;
using NotationParser = SoundScript.Parser.NotationParser;

namespace SoundScript.Tests;

public class NotationExtensionsTests
{
    [Fact]
    public void ParseRest_OccupiesTimeWithoutNotes()
    {
        const string source = """
            melody {
                C4 q
                rest e
                D4 q
            }
            """;

        var program = Parse(source);
        var interpreted = Interpreter.Interpret(program);
        var track = Assert.Single(interpreted.Tracks);

        Assert.Equal(2, track.Notes.Count);
        Assert.Equal(0.0, track.Notes[0].StartBeat);
        Assert.Equal(1.5, track.Notes[1].StartBeat);
        Assert.Contains(program.Statements.OfType<MelodyNode>().Single().Body, s => s is RestNode);
    }

    [Fact]
    public void ParseTie_MergesDurationsIntoSingleMidiNote()
    {
        const string source = """
            melody {
                C5 q ~ C5 q
            }
            """;

        var program = Parse(source);
        var note = program.Statements.OfType<MelodyNode>().Single().Body.OfType<NoteNode>().Single();

        Assert.True(note.Notation.IsTied);
        Assert.Equal(2.0, note.Notation.DurationBeats);

        var interpreted = Interpreter.Interpret(program);
        var track = Assert.Single(interpreted.Tracks);
        var timed = Assert.Single(track.Notes);
        Assert.Equal(72, timed.MidiNumber);
        Assert.Equal(2.0, timed.DurationBeats);
    }

    [Fact]
    public void ParseTie_RejectsDifferentPitches()
    {
        const string source = "melody { C5 q ~ D5 q }";
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(source));
        Assert.Contains("Invalid tie: pitches differ", ex.Message);
    }

    [Theory]
    [InlineData("staccato C4 q", ArticulationType.Staccato, 0.47)]
    [InlineData("C4 q legato", ArticulationType.Legato, 0.97)]
    [InlineData("accent C4 q", ArticulationType.Accent, 1.02)]
    public void ParseArticulation_AdjustsPlayback(string line, ArticulationType expected, double playbackBeats)
    {
        var program = Parse($"melody {{ {line} }}");
        var note = program.Statements.OfType<MelodyNode>().Single().Body.OfType<NoteNode>().Single();
        Assert.Equal(expected, note.Notation.Articulation);

        var interpreted = Interpreter.Interpret(program);
        var timed = Assert.Single(interpreted.Tracks.Single().Notes);
        Assert.Equal(playbackBeats, timed.DurationBeats);
    }

    [Fact]
    public void ParseDynamic_AppliesUntilChanged()
    {
        const string source = """
            melody {
                p
                C4 q
                mp
                D4 q
                mf
                E4 q
            }
            """;

        var program = Parse(source);
        var notes = Interpreter.Interpret(program).Tracks.Single().Notes;

        Assert.Equal(
            PlaybackShaper.ShapeNote(null, null, DynamicLevel.Piano, DynamicLevel.Piano, 64, null, null, 1.0).Velocity,
            notes[0].Velocity);
        Assert.Equal(
            PlaybackShaper.ShapeNote(null, null, DynamicLevel.MezzoPiano, DynamicLevel.MezzoPiano, 64, null, null, 1.0).Velocity,
            notes[1].Velocity);
        Assert.Equal(
            PlaybackShaper.ShapeNote(null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, null, 1.0).Velocity,
            notes[2].Velocity);
    }

    [Fact]
    public void ValidateMeasure_ReportsIncompleteAndExcess()
    {
        const string source = """
            time 4/4
            melody {
                C4 q E4 q G4 q |
                C4 h |
                C4 w |
                C4 q C4 q C4 q C4 q C4 q |
            }
            """;

        var interpreted = Interpreter.Interpret(Parse(source));

        Assert.Contains(interpreted.Warnings, w => w.Contains("Measure 1 incomplete"));
        Assert.Contains(interpreted.Warnings, w => w.Contains("Measure 2 incomplete"));
        Assert.Contains(interpreted.Warnings, w => w.Contains("Measure 4 exceeds expected duration"));
    }

    [Theory]
    [InlineData("sharpstaccato C4 q", "Unknown articulation: 'sharpstaccato'")]
    [InlineData("fff", "Unknown dynamic marking: 'fff'")]
    [InlineData("rest qq", "Invalid rest duration: 'rest qq'")]
    public void Parser_RejectsInvalidNotationExtensions(string script, string expectedMessage)
    {
        var source = script.StartsWith("melody", StringComparison.Ordinal)
            ? script
            : script.StartsWith("rest ", StringComparison.Ordinal) || script == "fff"
                ? $"melody {{ {script} }}"
                : $"melody {{ {script} }}";

        var ex = Assert.Throws<InvalidOperationException>(() => Parse(source));
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void ExistingExamples_RemainValid()
    {
        var examplePaths = ExampleTestHelpers.EnumerateMidiCompatibleExamples().ToList();

        foreach (var path in examplePaths)
        {
            var loaded = SoundScript.Parser.ProgramLoader.Load(path);
            var interpreted = Interpreter.Interpret(loaded.Program);
            Assert.True(interpreted.Tracks.Count > 0, $"Expected notes in {Path.GetFileName(path)}");
        }
    }

    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
}
