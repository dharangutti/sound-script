using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class PatternGenerationTests
{
    [Fact]
    public void Parse_PatternDefinition()
    {
        const string source = """
            pattern arp {
                up
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var pattern = Assert.IsType<PatternNode>(program.Statements[0]);

        Assert.Equal("arp", pattern.Name);
        Assert.Equal(PatternKind.Arpeggio, pattern.Kind);
        Assert.Equal(PatternDirection.Up, pattern.Direction);
    }

    [Fact]
    public void Parse_PatternPlayWithChord()
    {
        const string source = """
            track melody {
                play arp Cmaj q
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        var play = Assert.IsType<PlayNode>(track.Body[0]);

        Assert.Equal("arp", play.SequenceName);
        Assert.NotNull(play.PatternChord);
        Assert.Equal(ChordQuality.Major, play.PatternChord!.Quality);
    }

    [Fact]
    public void Expand_ArpeggioUp_ProducesAscendingNotes()
    {
        const string source = """
            pattern arp {
                up
            }
            track melody {
                play arp Cmaj q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(3, notes.Count);
        Assert.Equal([60, 64, 67], notes.Select(note => note.MidiNumber).ToArray());
        Assert.All(notes, note => Assert.Equal(1.0 / 3.0, note.DurationBeats, 3));
    }

    [Fact]
    public void Expand_ArpeggioDown_ProducesDescendingNotes()
    {
        const string source = """
            pattern arp {
                down
            }
            track melody {
                play arp Cmaj q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal([67, 64, 60], notes.Select(note => note.MidiNumber).ToArray());
    }

    [Fact]
    public void Expand_ArpeggioUpDown_ProducesPalindromeOrder()
    {
        const string source = """
            pattern arp {
                updown
            }
            track melody {
                play arp Cmaj q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal([60, 64, 67, 64, 60], notes.Select(note => note.MidiNumber).ToArray());
    }

    [Fact]
    public void Expand_Strum_StaggersChordTones()
    {
        const string source = """
            pattern strumPat {
                strum
            }
            track melody {
                play strumPat Cmaj q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;

        Assert.Equal(3, notes.Count);
        Assert.Equal(0.0, notes[0].StartBeat, 3);
        Assert.Equal(0.05, notes[1].StartBeat, 3);
        Assert.Equal(0.10, notes[2].StartBeat, 3);
        Assert.All(notes, note => Assert.Equal(1.0, note.DurationBeats));
    }

    [Fact]
    public void Expand_RhythmPattern_AppliesCustomDurations()
    {
        const string source = """
            pattern rhythm8 {
                rhythm e e q
            }
            track melody {
                play rhythm8 Cmaj h
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;

        Assert.Equal(3, notes.Count);
        Assert.Equal([0.5, 0.5, 1.0], notes.Select(note => note.DurationBeats).ToArray());
        Assert.Equal(2.0, notes.Sum(note => note.DurationBeats));
    }

    [Fact]
    public void Interpret_AppliesMusicalIntelligenceToExpandedNotes()
    {
        const string source = """
            pattern arp {
                up
            }
            track melody {
                C6 q
                play arp Cmaj q
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(5, notes.Count);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Octave smoothing applied"));
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
