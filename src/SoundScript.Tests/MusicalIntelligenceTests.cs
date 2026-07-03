using SoundScript.Core;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class MusicalIntelligenceTests
{
    [Fact]
    public void OctaveSmoothing_ReducesExtremeOctaveJumps()
    {
        const string source = """
            melody {
                C4 q
                C6 q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal(60, notes[0].MidiNumber);
        Assert.Equal(72, notes[1].MidiNumber);
    }

    [Fact]
    public void MelodicContour_ReducesWideLeaps()
    {
        const string source = """
            melody {
                C4 q
                A4 q
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal(60, notes[0].MidiNumber);
        Assert.Equal(57, notes[1].MidiNumber);
    }

    [Fact]
    public void HarmonicSpacing_AdjustsBrightChords()
    {
        const string source = """
            melody {
                Cmaj6 h
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Contains(notes.Select(n => n.MidiNumber), midi => midi == 79);
        Assert.Contains(interpreted.Warnings, w => w.Contains("Harmonic spacing adjustment applied"));
    }

    [Fact]
    public void PhraseSmoothing_SmoothsSequenceBoundaries()
    {
        const string source = """
            sequence intro {
                C6 q
            }

            track melody {
                play intro
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
    public void DynamicRamp_SoftensAbruptDynamicChanges()
    {
        const string source = """
            melody {
                p
                f
                C4 q
                D4 q
                E4 q
            }
            """;

        var velocities = Interpret(source).Tracks.Single().Notes.Select(n => n.Velocity).ToList();

        Assert.Equal(64, velocities[0]);
        Assert.Equal(80, velocities[1]);
        Assert.Equal(96, velocities[2]);
        Assert.Contains(Interpret(source).Warnings, w => w.Contains("Dynamic ramp applied"));
    }

    [Fact]
    public void NotatedNote_TracksPhraseAndAdjustedMidi()
    {
        var note = new NotatedNote
        {
            PitchClass = PitchClass.C,
            Octave = 6,
            PhraseIndex = 2,
            AdjustedOctave = 5,
            AdjustedMidiNumber = 72
        };

        Assert.Equal(72, note.ResolvedMidiNumber);
        Assert.Equal(2, note.PhraseIndex);
        Assert.Equal(5, note.EffectiveOctave);
    }

    [Fact]
    public void ExistingExamples_RemainValid()
    {
        var examplePaths = Directory.GetFiles(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples")),
            "*.ss");

        foreach (var path in examplePaths)
        {
            var interpreted = Interpret(File.ReadAllText(path));
            Assert.True(interpreted.Tracks.Count > 0, $"Expected notes in {Path.GetFileName(path)}");
        }
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
