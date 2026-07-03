using SoundScript.Core;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class HumanizeTests
{
    [Fact]
    public void Interpret_ProducesDeterministicJitterForSameSeed()
    {
        const string source = """
            tempo 120
            track piano {
                humanize 0.03
                mf
                C4 q
                D4 q
            }
            """;

        HumanizeApplicator.SetSeed(42);
        var first = Interpret(source);
        HumanizeApplicator.SetSeed(42);
        var second = Interpret(source);

        var firstNotes = first.Tracks.Single().Notes;
        var secondNotes = second.Tracks.Single().Notes;

        Assert.Equal(firstNotes.Count, secondNotes.Count);
        for (var i = 0; i < firstNotes.Count; i++)
        {
            Assert.Equal(firstNotes[i].StartBeat, secondNotes[i].StartBeat);
            Assert.Equal(firstNotes[i].Velocity, secondNotes[i].Velocity);
        }
    }

    [Fact]
    public void Interpret_AppliesTimingJitterBeforeMidiEmission()
    {
        const string source = """
            tempo 120
            track piano {
                humanize 0.03
                C4 q
            }
            """;

        HumanizeApplicator.SetSeed(42);
        var expectedStart = HumanizeApplicator.ApplyToStartBeat(0, 0.03, 120, 0);
        var humanized = Interpret(source).Tracks.Single().Notes.Single();
        var dry = Interpret("track piano { C4 q }").Tracks.Single().Notes.Single();

        Assert.Equal(0, dry.StartBeat);
        Assert.Equal(expectedStart, humanized.StartBeat);
        Assert.Equal(1.0, humanized.DurationBeats);
    }

    [Fact]
    public void Interpret_AppliesVelocityJitterAfterShaping()
    {
        const string source = """
            track piano {
                instrument piano
                humanize 0.03
                mf
                C4 q
            }
            """;

        var shaped = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "piano", 1.0).Velocity;

        HumanizeApplicator.SetSeed(99);
        var expectedVelocity = HumanizeApplicator.ApplyVelocity(shaped, 0.03, 0);
        var humanized = Interpret(source).Tracks.Single().Notes.Single();
        var dry = Interpret("""
            track piano {
                instrument piano
                mf
                C4 q
            }
            """).Tracks.Single().Notes.Single();

        Assert.Equal(shaped, dry.Velocity);
        Assert.NotEqual(shaped, expectedVelocity);
        Assert.Equal(expectedVelocity, humanized.Velocity);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
