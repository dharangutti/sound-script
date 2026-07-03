using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class LayerTests
{
    [Fact]
    public void Interpret_DuplicatesNotesPerLayer()
    {
        const string source = """
            track piano {
                layer piano
                layer flute
                mf
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var track = Assert.Single(interpreted.Tracks);

        Assert.Equal(2, track.Notes.Count);
        Assert.All(track.Notes, note => Assert.Equal(60, note.MidiNumber));
        Assert.Contains(track.Notes, note => note.Channel == 0);
        Assert.Contains(track.Notes, note => note.Channel == 1);
    }

    [Fact]
    public void Interpret_ShapesEachLayerIndependently()
    {
        const string source = """
            track lead {
                layer piano
                layer flute
                mf
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes.OrderBy(note => note.Channel).ToList();

        var expectedPiano = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "piano", 1.0).Velocity;
        var expectedFlute = PlaybackShaper.ShapeNote(
            null, null, DynamicLevel.MezzoForte, DynamicLevel.MezzoForte, 64, null, "flute", 1.0).Velocity;

        Assert.Equal(expectedPiano, notes[0].Velocity);
        Assert.Equal(expectedFlute, notes[1].Velocity);
        Assert.NotEqual(notes[0].Velocity, notes[1].Velocity);
    }

    [Fact]
    public void MidiGenerator_AssignsProgramPerLayerChannel()
    {
        const string source = """
            track piano {
                layer piano
                layer violin
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var track = interpreted.Tracks.Single();
        Assert.Equal(2, track.ProgramChanges.Count);
        Assert.Contains(track.ProgramChanges, change => change.Channel == 0 && change.ProgramNumber == 0);
        Assert.Contains(track.ProgramChanges, change => change.Channel == 1 && change.ProgramNumber == 40);

        var path = Path.Combine(Path.GetTempPath(), $"soundscript-layer-{Guid.NewGuid():N}.mid");

        try
        {
            MidiGenerator.Write(interpreted, path);
            var midiFile = Melanchall.DryWetMidi.Core.MidiFile.Read(path);
            var notes = midiFile.GetNotes().ToList();

            Assert.Equal(2, notes.Count);
            Assert.Contains(notes, note => note.Channel == 0);
            Assert.Contains(notes, note => note.Channel == 1);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Parse_LayerStatement()
    {
        const string source = """
            track piano {
                layer piano
                layer flute
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        var layers = track.Body.OfType<LayerNode>().ToList();

        Assert.Equal(2, layers.Count);
        Assert.Equal("piano", layers[0].Name);
        Assert.Equal("flute", layers[1].Name);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
