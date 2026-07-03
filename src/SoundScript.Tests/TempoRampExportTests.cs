using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class TempoRampExportTests
{
    [Fact]
    public void TempoMap_BeatAccurateIntegration_MatchesAnalyticRamp()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 4 bars
            """;

        var interpreted = Interpret(source);
        var map = interpreted.TempoMap;

        for (var beat = 0.0; beat <= 16.0; beat += 0.5)
        {
            var expectedBpm = 120 + (140 - 120) * (beat / 16.0);
            Assert.Equal(expectedBpm, map.GetBpmAt(beat), 3);
        }

        var quarterAtStart = map.BeatsToMilliseconds(0, 1);
        var quarterAtMid = map.BeatsToMilliseconds(8, 1);
        var quarterAtEnd = map.BeatsToMilliseconds(15, 1);

        Assert.True(quarterAtMid < quarterAtStart);
        Assert.True(quarterAtEnd < quarterAtMid);

        var wholeRamp = map.BeatsToMilliseconds(0, 16);
        var summedQuarters =
            map.BeatsToMilliseconds(0, 4)
            + map.BeatsToMilliseconds(4, 4)
            + map.BeatsToMilliseconds(8, 4)
            + map.BeatsToMilliseconds(12, 4);
        Assert.Equal(wholeRamp, summedQuarters, 3);
    }

    [Fact]
    public void MidiExport_TempoEvents_MatchExpectedBeatTimestamps()
    {
        const string source = """
            time 4/4
            tempo 120 → 140 over 4 bars
            track melody {
                C4 w
            }
            """;

        var interpreted = Interpret(source);
        var path = Path.Combine(Path.GetTempPath(), $"soundscript-tempo-export-{Guid.NewGuid():N}.mid");

        try
        {
            MidiGenerator.Write(interpreted, path);
            var midiFile = Melanchall.DryWetMidi.Core.MidiFile.Read(path);
            var tempoMap = midiFile.GetTempoMap();
            var tempoChanges = tempoMap.GetTempoChanges().ToList();

            var expectedPoints = interpreted.TempoMap.GetTempoMapPoints().OrderBy(point => point.Beat).ToList();
            Assert.NotEmpty(expectedPoints);

            foreach (var point in expectedPoints.Where(point => point.Beat > 0))
            {
                var expectedTick = (long)Math.Round(point.Beat * 480);
                Assert.True(
                    tempoChanges.Any(change =>
                        Math.Abs(change.Time - expectedTick) <= 1
                        && Math.Abs(change.Value.BeatsPerMinute - point.Bpm) < 0.5),
                    $"Missing tempo change at beat {point.Beat} ({expectedTick} ticks) for {point.Bpm} BPM");
            }
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
