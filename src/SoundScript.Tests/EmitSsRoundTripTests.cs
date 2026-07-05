using Melanchall.DryWetMidi.Interaction;
using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Prosody;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class EmitSsRoundTripTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void PhonemeComposer_PrintThenReparse_ProducesIdenticalMidiBytes()
    {
        var ast = PhonemeComposer.BuildAst(Example);
        Assert.Equal(RenderDirect(ast), RenderViaPrintedSs(ast));
    }

    [Fact]
    public void ProsodyComposer_PrintThenReparse_ProducesIdenticalMidiBytes()
    {
        var ast = ProsodyComposer.BuildAst(Example);
        Assert.Equal(RenderDirect(ast), RenderViaPrintedSs(ast));
    }

    [Fact]
    public void Print_EmitsComposedBpmAsFirstStatement()
    {
        var ast = PhonemeComposer.BuildAst(Example, tempo: 96);
        var text = SsPrinter.Print(ast);

        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
        Assert.Equal("tempo 96", firstLine);

        var reinterpreted = Reparse(text);
        var path = Path.Combine(Path.GetTempPath(), $"soundscript-emit-ss-tempo-{Guid.NewGuid():N}.mid");
        try
        {
            MidiGenerator.Write(reinterpreted, path);
            var midiFile = Melanchall.DryWetMidi.Core.MidiFile.Read(path);
            var tempoChanges = midiFile.GetTempoMap().GetTempoChanges().ToList();

            Assert.Single(tempoChanges);
            Assert.Equal(96, tempoChanges[0].Value.BeatsPerMinute, 3);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Print_IsHumanFormattedNotASingleLineDump()
    {
        var ast = PhonemeComposer.BuildAst(Example);
        var text = SsPrinter.Print(ast);

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 5, "Expected multiple lines of formatted output.");
        Assert.Contains(lines, line => line.StartsWith("    ", StringComparison.Ordinal));
    }

    [Fact]
    public void Print_HandEditOfOneNote_ChangesOnlyThatNoteWhenReparsed()
    {
        var ast = PhonemeComposer.BuildAst("star");
        var text = SsPrinter.Print(ast);

        var originalNotes = Reparse(text).Tracks.Single().Notes;

        // Bump the velocity of the first note that has one (v<N>) by 1, simulating a hand edit.
        var lines = text.Split('\n');
        var editedIndex = Array.FindIndex(lines, l => l.Contains(" v", StringComparison.Ordinal));
        Assert.True(editedIndex >= 0, "Expected at least one note with an explicit velocity to edit.");

        var vIndex = lines[editedIndex].IndexOf(" v", StringComparison.Ordinal);
        var prefix = lines[editedIndex][..(vIndex + 2)];
        var digits = lines[editedIndex][(vIndex + 2)..];
        var newVelocity = int.Parse(digits) + 1;
        lines[editedIndex] = prefix + newVelocity;

        var editedText = string.Join('\n', lines);
        var editedNotes = Reparse(editedText).Tracks.Single().Notes;

        Assert.Equal(originalNotes.Count, editedNotes.Count);

        var differences = 0;
        for (var i = 0; i < originalNotes.Count; i++)
        {
            if (!originalNotes[i].Equals(editedNotes[i]))
                differences++;
        }

        Assert.Equal(1, differences);
    }

    [Fact]
    public void Print_ThrowsOnTiedNote()
    {
        var program = new ProgramNode();
        var track = new TrackNode { Name = "melody" };
        var phrase = new PhraseNode();
        phrase.Body.Add(new NoteNode
        {
            Notation = new NotatedNote { PitchClass = PitchClass.C, Octave = 4, DurationBeats = 2.0, IsTied = true }
        });
        track.Body.Add(phrase);
        program.Statements.Add(track);

        Assert.Throws<NotSupportedException>(() => SsPrinter.Print(program));
    }

    [Fact]
    public void Print_ThrowsOnNoneEnvelope()
    {
        var program = new ProgramNode();
        var track = new TrackNode { Name = "melody" };
        var phrase = new PhraseNode();
        phrase.Body.Add(new PhraseEnvelopeNode { Envelope = PhraseEnvelopeType.None });
        track.Body.Add(phrase);
        program.Statements.Add(track);

        Assert.Throws<NotSupportedException>(() => SsPrinter.Print(program));
    }

    [Fact]
    public void Print_ThrowsOnUnsupportedNodeType()
    {
        var program = new ProgramNode();
        program.Statements.Add(new BarNode(1));

        Assert.Throws<NotSupportedException>(() => SsPrinter.Print(program));
    }

    private static byte[] RenderDirect(ProgramNode ast)
    {
        var interpreted = Interpreter.Interpret(ast);
        using var stream = new MemoryStream();
        MidiGenerator.Write(interpreted, stream);
        return stream.ToArray();
    }

    private static byte[] RenderViaPrintedSs(ProgramNode ast)
    {
        var text = SsPrinter.Print(ast);
        var interpreted = Reparse(text);
        using var stream = new MemoryStream();
        MidiGenerator.Write(interpreted, stream);
        return stream.ToArray();
    }

    private static InterpretedProgram Reparse(string text)
    {
        var program = new SoundScriptParser(new Tokenizer(text).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
