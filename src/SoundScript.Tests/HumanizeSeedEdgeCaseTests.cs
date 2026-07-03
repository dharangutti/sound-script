using SoundScript.Core;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class HumanizeSeedEdgeCaseTests
{
    [Fact]
    public void Humanize_SingleTrack_IsDeterministicAcrossRuns()
    {
        const string source = """
            track piano {
                humanize 0.03
                C4 q
                D4 q
            }
            """;

        HumanizeApplicator.SetSeed(42);
        var first = Interpret(source);
        HumanizeApplicator.SetSeed(42);
        var second = Interpret(source);

        AssertEquivalentNotes(first.Tracks.Single().Notes, second.Tracks.Single().Notes);
    }

    [Fact]
    public void Humanize_MultiTrack_UsesGlobalNoteIndexAcrossTracks()
    {
        const string source = """
            track melody {
                humanize 0.03
                C5 q
            }
            track bass {
                humanize 0.03
                C2 q
            }
            """;

        HumanizeApplicator.SetSeed(77);
        var interpreted = Interpret(source);
        var melody = interpreted.Tracks.Single(t => t.Name == "melody").Notes.Single();
        var bass = interpreted.Tracks.Single(t => t.Name == "bass").Notes.Single();

        Assert.NotEqual(melody.Velocity, bass.Velocity);
    }

    [Fact]
    public void Humanize_LayeredTrack_UsesDistinctChannelsForSeed()
    {
        const string source = """
            track piano {
                layer piano
                layer cello
                humanize 0.03
                mf
                C4 q
            }
            """;

        HumanizeApplicator.SetSeed(55);
        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal(2, notes.Count);
        Assert.NotEqual(notes[0].Velocity, notes[1].Velocity);
    }

    [Fact]
    public void Humanize_BlockExpansion_ProducesDeterministicSequence()
    {
        const string source = """
            block motif { C4 q D4 q }
            track piano {
                humanize 0.03
                play motif
                play motif
            }
            """;

        HumanizeApplicator.SetSeed(11);
        var first = Interpret(source).Tracks.Single().Notes;
        HumanizeApplicator.SetSeed(11);
        var second = Interpret(source).Tracks.Single().Notes;

        AssertEquivalentNotes(first, second);
        Assert.Equal(4, first.Count);
        Assert.NotEqual(first[0].StartBeat, first[1].StartBeat);
        Assert.NotEqual(first[2].StartBeat, first[3].StartBeat);
    }

    private static void AssertEquivalentNotes(
        IReadOnlyList<TimedNote> left,
        IReadOnlyList<TimedNote> right)
    {
        Assert.Equal(left.Count, right.Count);
        for (var i = 0; i < left.Count; i++)
        {
            Assert.Equal(left[i].StartBeat, right[i].StartBeat);
            Assert.Equal(left[i].Velocity, right[i].Velocity);
        }
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
