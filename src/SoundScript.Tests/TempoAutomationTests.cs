using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class TempoAutomationTests
{
    [Fact]
    public void TempoMap_ComputesLinearRampBpm()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 4 bars
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var interpreted = Interpreter.Interpret(program);

        Assert.Equal(120, interpreted.TempoMap.GetBpmAt(0));
        Assert.Equal(130, interpreted.TempoMap.GetBpmAt(8), 6);
        Assert.Equal(140, interpreted.TempoMap.GetBpmAt(16));
    }

    [Fact]
    public void Interpret_AdjustsDurationDuringLinearRamp()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 4 bars
            track melody {
                C4 w
            }
            """;

        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();
        var constantMs = new TempoAutomationMap().BeatsToMilliseconds(0, 4);
        var rampMs = interpreted.TempoMap.BeatsToMilliseconds(0, 4);

        Assert.True(rampMs < constantMs);
        Assert.Equal(rampMs, note.DurationMs);
    }

    [Fact]
    public void Interpret_SupportsMultipleTempoRamps()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 2 bars
            tempo 140 → 100 over 2 bars
            track melody {
                C4 h
                D4 h
            }
            """;

        var interpreted = Interpret(source);
        Assert.Equal(120, interpreted.TempoMap.GetBpmAt(12), 6);
        Assert.Equal(100, interpreted.TempoMap.GetBpmAt(16));
        Assert.Equal(2, interpreted.Tracks.Single().Notes.Count);
    }

    [Fact]
    public void GetTempoMapPoints_ReflectsAConstantTempoSetAtBeatZero()
    {
        // Regression: GetTempoMapPoints() used to hard-code a (0, InitialBpm) point
        // ahead of the loop and only emitted a segment's own beat-0 point for ramps,
        // so a plain `tempo N` statement (a ConstantTempoSegment starting at beat 0)
        // was silently dropped in favor of the stale 120 BPM default — every MIDI
        // file the composers wrote played back at 120 BPM regardless of the
        // requested tempo, unless a beat-0 ramp happened to overwrite it too.
        var map = new TempoAutomationMap();
        map.SetTempo(0, 96);

        var points = map.GetTempoMapPoints().ToList();

        Assert.Single(points);
        Assert.Equal(0, points[0].Beat);
        Assert.Equal(96, points[0].Bpm);
    }

    [Fact]
    public void MidiGenerator_ExportsTheComposedTempoNotTheStaleDefault()
    {
        // End-to-end version of the regression above, through the real MIDI writer.
        const string source = """
            tempo 96
            track melody {
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var path = Path.Combine(Path.GetTempPath(), $"soundscript-tempo-beat0-{Guid.NewGuid():N}.mid");

        try
        {
            MidiGenerator.Write(interpreted, path);
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
    public void MidiGenerator_ExportsTempoMapChanges()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 2 bars
            tempo 140 → 100 over 2 bars
            track melody {
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var path = Path.Combine(Path.GetTempPath(), $"soundscript-tempo-{Guid.NewGuid():N}.mid");

        try
        {
            MidiGenerator.Write(interpreted, path);
            var midiFile = Melanchall.DryWetMidi.Core.MidiFile.Read(path);
            var tempoChanges = midiFile.GetTempoMap().GetTempoChanges().ToList();

            Assert.True(tempoChanges.Count >= 3);
            Assert.Contains(tempoChanges, change => (int)Math.Round(change.Value.BeatsPerMinute) == 120);
            Assert.Contains(tempoChanges, change => (int)Math.Round(change.Value.BeatsPerMinute) == 140);
            Assert.Contains(tempoChanges, change => (int)Math.Round(change.Value.BeatsPerMinute) == 100);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Parse_TempoRampStatement()
    {
        const string source = "tempo 120 -> 140 over 4 bars";
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var ramp = Assert.IsType<TempoRampNode>(program.Statements[0]);

        Assert.Equal(120, ramp.StartBpm);
        Assert.Equal(140, ramp.EndBpm);
        Assert.Equal(4, ramp.Bars);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
