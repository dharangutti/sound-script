using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class TrackMetadataTests
{
    [Fact]
    public void Interpret_AppliesTrackGainAfterPlaybackShaping()
    {
        const string source = """
            track piano {
                instrument piano
                gain 0.5
                mf
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();
        var shaped = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "piano", 1.0).Velocity;
        var expected = Math.Clamp((int)Math.Round(shaped * 0.5), 1, 127);

        Assert.Equal(expected, note.Velocity);
    }

    [Fact]
    public void Interpret_AppliesHumanizeToStartBeatOnly()
    {
        HumanizeApplicator.SetSeed(99);

        try
        {
            const string source = """
                tempo 120
                track piano {
                    humanize 0.03
                    C4 q
                    D4 q
                }
                """;

            var interpreted = Interpret(source);
            var notes = interpreted.Tracks.Single().Notes;

            HumanizeApplicator.SetSeed(99);
            var expectedStart0 = HumanizeApplicator.ApplyToStartBeat(0, 0.03, 120, 0);
            var expectedStart1 = HumanizeApplicator.ApplyToStartBeat(1, 0.03, 120, 1);

            Assert.Equal(expectedStart0, notes[0].StartBeat, 6);
            Assert.Equal(expectedStart1, notes[1].StartBeat, 6);
            Assert.Equal(1.0, notes[0].DurationBeats);
            Assert.Equal(1.0, notes[1].DurationBeats);
        }
        finally
        {
            HumanizeApplicator.SetSeed(null);
        }
    }

    [Fact]
    public void Interpret_AppliesIndependentMetadataPerTrack()
    {
        HumanizeApplicator.SetSeed(5);

        try
        {
            const string source = """
                tempo 60
                track soft {
                    instrument flute
                    gain 0.25
                    humanize 0.02
                    mf
                    C5 q
                }
                track dry {
                    instrument piano
                    gain 1.0
                    mf
                    C4 q
                }
                """;

            var interpreted = Interpret(source);
            var soft = interpreted.Tracks.Single(t => t.Name == "soft").Notes.Single();
            var dry = interpreted.Tracks.Single(t => t.Name == "dry").Notes.Single();

            var shapedPiano = PlaybackShaper.ShapeNote(
                null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "piano", 1.0).Velocity;

            Assert.Equal(shapedPiano, dry.Velocity);
            Assert.NotEqual(0.0, soft.StartBeat);
            Assert.Equal(0.0, dry.StartBeat);
        }
        finally
        {
            HumanizeApplicator.SetSeed(null);
        }
    }

    [Fact]
    public void Parse_TrackMetadataStatements()
    {
        const string source = """
            track piano {
                instrument piano
                gain 0.9
                humanize 0.03
                C4 q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        Assert.IsType<InstrumentNode>(track.Body[0]);
        Assert.Equal(0.9, Assert.IsType<GainNode>(track.Body[1]).Value);
        Assert.Equal(0.03, Assert.IsType<HumanizeNode>(track.Body[2]).Value);
    }

    [Fact]
    public void Parse_RejectsGainOutsideUnitInterval()
    {
        const string source = "track piano { gain 1.5 C4 q }";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse());
        Assert.Contains("Gain must be between 0.0 and 1.0", ex.Message);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
