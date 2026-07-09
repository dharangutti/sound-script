using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class StabilizationTests
{
    [Fact]
    public void InstrumentBalance_AppliesPerInstrumentGain()
    {
        const string source = """
            track lead {
                instrument flute
                mf
                C5 q
            }
            track rhythm {
                instrument piano
                mf
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var flute = interpreted.Tracks.Single(t => t.Name == "lead").Notes.Single();
        var piano = interpreted.Tracks.Single(t => t.Name == "rhythm").Notes.Single();

        var expectedFlute = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "flute", 1.0).Velocity;
        var expectedPiano = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "piano", 1.0).Velocity;

        Assert.Equal(expectedFlute, flute.Velocity);
        Assert.Equal(expectedPiano, piano.Velocity);
    }

    [Fact]
    public void VelocityCurve_AppliesSoftCurveForLegato()
    {
        const string source = """
            melody {
                C4 q legato
            }
            """;

        var interpreted = Interpret(source);
        var velocity = interpreted.Tracks.Single().Notes.Single().Velocity;
        var expected = PlaybackShaper.ShapeNote(
            null,
            null,
            null,
            null,
            64,
            ArticulationType.Legato,
            null,
            1.0).Velocity;

        Assert.Equal(expected, velocity);
    }

    [Fact]
    public void TimingPrecision_AvoidsDriftAcrossManyEvents()
    {
        const string source = """
            track timing {
                loop 4 {
                    C4 e
                    D4 e
                    E4 e
                    F4 e
                }
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;
        var last = notes[^1];

        Assert.Equal(7.5, last.StartBeat);
        Assert.Equal(8.0, last.StartBeat + last.DurationBeats);
    }

    [Fact]
    public void ChordVoicing_AdjustsLowRootsAndSpreadsWideChords()
    {
        const string source = """
            melody {
                Cmaj2 h
                G7 h
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.All(notes.Take(3), n => Assert.True(n.MidiNumber >= 48));
        Assert.Contains(interpreted.Warnings, w => w.Contains("Chord voicing adjustment applied"));
    }

    [Fact]
    public void LoopAlignment_SnapsBeatCursorToExactBoundary()
    {
        const string source = """
            track loops {
                loop 3 {
                    C4 e
                    D4 e
                    E4 e
                }
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(4.5, notes[^1].StartBeat + notes[^1].DurationBeats);
    }

    [Fact]
    public void SequenceInheritance_UsesParentTrackInstrument()
    {
        const string source = """
            sequence intro {
                C4 q
            }

            track melody {
                instrument flute
                play intro
            }
            """;

        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();
        var expected = PlaybackShaper.ShapeNote(
            null, null, null, null, 64, null, "flute", 1.0).Velocity;

        Assert.Equal(expected, note.Velocity);
        Assert.Contains(interpreted.Warnings, w => w.Contains("Sequence inherited instrument: flute"));
    }

    [Fact]
    public void MultiTrackSync_AlignsStartBeatsOnGlobalClock()
    {
        const string source = """
            track melody {
                C5 q
            }
            track bass {
                C2 q
            }
            """;

        var interpreted = Interpret(source);
        var melodyStart = interpreted.Tracks.Single(t => t.Name == "melody").Notes.Single().StartBeat;
        var bassStart = interpreted.Tracks.Single(t => t.Name == "bass").Notes.Single().StartBeat;

        Assert.Equal(0.0, melodyStart);
        Assert.Equal(0.0, bassStart);
        Assert.Equal(melodyStart, bassStart);
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

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
