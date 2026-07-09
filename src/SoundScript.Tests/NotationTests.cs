using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;
using NotationParser = SoundScript.Parser.NotationParser;

namespace SoundScript.Tests;

public class NotationParserTests
{
    private static Token Token(string value, int line = 1, int column = 1) =>
        new(TokenType.Note, value, line, column);

    [Theory]
    [InlineData("C#4", PitchClass.C, AccidentalType.Sharp, 4, 61)]
    [InlineData("Db4", PitchClass.D, AccidentalType.Flat, 4, 61)]
    [InlineData("F#3", PitchClass.F, AccidentalType.Sharp, 3, 54)]
    [InlineData("Bb3", PitchClass.B, AccidentalType.Flat, 3, 58)]
    [InlineData("C\u266E4", PitchClass.C, AccidentalType.Natural, 4, 60)]
    public void ParsePitchWithAccidental_AcceptsValidNotes(
        string text,
        PitchClass expectedPitch,
        AccidentalType expectedAccidental,
        int expectedOctave,
        int expectedMidi)
    {
        var (pitch, accidental, octave) = NotationParser.ParsePitchWithAccidental(text, Token(text));

        Assert.Equal(expectedPitch, pitch);
        Assert.Equal(expectedAccidental, accidental);
        Assert.Equal(expectedOctave, octave);

        var note = NotationParser.BuildNotatedNote(pitch, accidental, octave, 1.0);
        Assert.Equal(expectedMidi, note.ToMidiNumber());
    }

    [Theory]
    [InlineData("H5", "Unknown pitch name: H")]
    [InlineData("C9", "Invalid octave: 9")]
    [InlineData("C#-1", "Invalid octave: -1")]
    [InlineData("Z4", "Unknown pitch name: Z")]
    [InlineData("C##4", "Invalid accidental syntax: 'C##4'")]
    public void ParsePitchWithAccidental_RejectsInvalidNotes(string text, string expectedMessage)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotationParser.ParsePitchWithAccidental(text, Token(text)));

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData("q", NoteDuration.Quarter, 1.0)]
    [InlineData("quarter", NoteDuration.Quarter, 1.0)]
    [InlineData("h", NoteDuration.Half, 2.0)]
    [InlineData("half", NoteDuration.Half, 2.0)]
    [InlineData("e", NoteDuration.Eighth, 0.5)]
    [InlineData("eighth", NoteDuration.Eighth, 0.5)]
    [InlineData("w", NoteDuration.Whole, 4.0)]
    [InlineData("whole", NoteDuration.Whole, 4.0)]
    public void ParseDurationAlias_NormalizesDurations(string token, NoteDuration expected, double beats)
    {
        var durationToken = new Token(TokenType.Duration, token, 1, 1);
        var (standardDuration, parsedBeats) = NotationParser.ParseDurationAlias(token, durationToken);

        Assert.Equal(expected, standardDuration);
        Assert.Equal(beats, parsedBeats);
    }

    [Theory]
    [InlineData("qq")]
    [InlineData("quartereighth")]
    public void ParseDurationAlias_RejectsInvalidDurations(string token)
    {
        var durationToken = new Token(TokenType.Duration, token, 1, 1);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotationParser.ParseDurationAlias(token, durationToken));

        Assert.Contains($"Unknown duration: '{token}'", ex.Message);
    }
}

public class NotationIntegrationTests
{
    [Fact]
    public void ParseAndInterpret_AccidentalAndAliasDurations()
    {
        const string source = """
            melody {
                C#4 q
                Db4 half
                F#3 eighth
                Bb3 whole
            }
            """;

        var program = new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
        var melody = Assert.Single(program.Statements.OfType<MelodyNode>());
        var notes = melody.Body.OfType<NoteNode>().ToList();

        Assert.Equal(4, notes.Count);
        Assert.Equal(61, notes[0].Notation.ToMidiNumber());
        Assert.Equal(NoteDuration.Quarter, notes[0].Notation.StandardDuration);
        Assert.Equal(61, notes[1].Notation.ToMidiNumber());
        Assert.Equal(NoteDuration.Half, notes[1].Notation.StandardDuration);
        Assert.Equal(54, notes[2].Notation.ToMidiNumber());
        Assert.Equal(NoteDuration.Eighth, notes[2].Notation.StandardDuration);
        Assert.Equal(58, notes[3].Notation.ToMidiNumber());
        Assert.Equal(NoteDuration.Whole, notes[3].Notation.StandardDuration);

        var interpreted = Interpreter.Interpret(program);
        var track = Assert.Single(interpreted.Tracks);
        Assert.Equal(4, track.Notes.Count);
        Assert.Equal(0.0, track.Notes[0].StartBeat);
        Assert.Equal(1.0, track.Notes[1].StartBeat);
        Assert.Equal(3.0, track.Notes[2].StartBeat);
        Assert.Equal(3.5, track.Notes[3].StartBeat);
    }

    [Theory]
    [InlineData("H5 q", "Unknown pitch name: H")]
    [InlineData("C9 q", "Invalid octave: 9")]
    [InlineData("C#-1 q", "Invalid octave: -1")]
    [InlineData("Z#4 h", "Unknown pitch name: Z")]
    [InlineData("C##4 q", "Invalid accidental syntax: 'C##4'")]
    public void Parser_RejectsInvalidNotationScripts(string noteLine, string expectedMessage)
    {
        var source = $"melody {{ {noteLine} }}";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse());

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void ExistingExamples_RemainValid()
    {
        var examplePaths = ExampleTestHelpers.EnumerateMidiCompatibleExamples().ToList();

        Assert.NotEmpty(examplePaths);

        foreach (var path in examplePaths)
        {
            var loaded = SoundScript.Parser.ProgramLoader.Load(path);
            var interpreted = Interpreter.Interpret(loaded.Program);
            Assert.True(interpreted.Tracks.Count > 0, $"Expected notes in {Path.GetFileName(path)}");
        }
    }
}
