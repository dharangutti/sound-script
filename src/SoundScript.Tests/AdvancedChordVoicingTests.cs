using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class AdvancedChordVoicingTests
{
    [Fact]
    public void Apply_Drop2_LowersSecondVoiceFromTop()
    {
        var notes = new[] { 60, 64, 67, 71 };
        var (voiced, _) = AdvancedChordVoicing.Apply(notes, ChordVoicingStyle.Drop2);

        Assert.Equal([55, 60, 64, 71], voiced);
    }

    [Fact]
    public void Apply_Drop3_LowersThirdVoiceFromTop()
    {
        var notes = new[] { 60, 64, 67, 71 };
        var (voiced, _) = AdvancedChordVoicing.Apply(notes, ChordVoicingStyle.Drop3);

        Assert.Equal([52, 60, 67, 71], voiced);
    }

    [Fact]
    public void Apply_Inversion1_MovesRootUpAnOctave()
    {
        var notes = new[] { 60, 64, 67 };
        var (voiced, _) = AdvancedChordVoicing.Apply(notes, ChordVoicingStyle.Inversion1);

        Assert.Equal([64, 67, 72], voiced);
    }

    [Fact]
    public void Apply_Inversion2_MovesRootAndThirdUp()
    {
        var notes = new[] { 60, 64, 67 };
        var (voiced, _) = AdvancedChordVoicing.Apply(notes, ChordVoicingStyle.Inversion2);

        Assert.Equal([67, 72, 76], voiced);
    }

    [Fact]
    public void Apply_Spread_WidensUpperVoices()
    {
        var notes = new[] { 60, 64, 67 };
        var (voiced, _) = AdvancedChordVoicing.Apply(notes, ChordVoicingStyle.Spread);

        Assert.Equal([60, 76, 79], voiced);
    }

    [Fact]
    public void Parse_ChordVoicingModifier()
    {
        const string source = "track melody { Cmaj drop2 q }";
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        var chord = Assert.IsType<ChordNode>(track.Body[0]);

        Assert.Equal(ChordVoicingStyle.Drop2, chord.Voicing);
    }

    [Fact]
    public void Interpret_AppliesAdvancedVoicingInPipeline()
    {
        const string source = """
            track melody {
                Cmaj inv1 q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var interpreted = Interpreter.Interpret(program);

        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Advanced chord voicing applied"));
        Assert.Equal([64, 67, 72], interpreted.Tracks.Single().Notes.Select(note => note.MidiNumber).OrderBy(number => number));
    }
}
