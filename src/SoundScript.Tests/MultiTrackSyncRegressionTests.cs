using SoundScript.Core;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class MultiTrackSyncRegressionTests
{
    [Fact]
    public void MultiTrackSync_LongRun_StaysAlignedAt1000Beats()
    {
        const string source = """
            track melody {
                loop 500 {
                    C5 e
                    D5 e
                    E5 e
                    F5 e
                }
            }
            track bass {
                loop 500 {
                    C2 e
                    G2 e
                    A2 e
                    F2 e
                }
            }
            """;

        var interpreted = Interpret(source);
        var melody = interpreted.Tracks.Single(t => t.Name == "melody").Notes;
        var bass = interpreted.Tracks.Single(t => t.Name == "bass").Notes;

        Assert.Equal(2000, melody.Count);
        Assert.Equal(2000, bass.Count);

        var melodyEnd = melody[^1].StartBeat + melody[^1].DurationBeats;
        var bassEnd = bass[^1].StartBeat + bass[^1].DurationBeats;

        Assert.Equal(1000.0, melodyEnd);
        Assert.Equal(1000.0, bassEnd);
        Assert.Equal(melodyEnd, bassEnd);
    }

    [Fact]
    public void MultiTrackSync_DriftSimulation_KeepsEqualLengthTracksAligned()
    {
        const string source = """
            track lead {
                loop 100 {
                    C5 e
                    D5 e
                    E5 e
                    F5 e
                }
            }
            track follow {
                loop 100 {
                    C3 e
                    G3 e
                    A3 e
                    F3 e
                }
            }
            """;

        var interpreted = Interpret(source);
        var lead = interpreted.Tracks.Single(t => t.Name == "lead").Notes;
        var follow = interpreted.Tracks.Single(t => t.Name == "follow").Notes;

        Assert.Equal(400, lead.Count);
        Assert.Equal(400, follow.Count);
        Assert.Equal(0.0, lead[0].StartBeat);
        Assert.Equal(0.0, follow[0].StartBeat);
        Assert.Equal(200.0, lead[^1].StartBeat + lead[^1].DurationBeats);
        Assert.Equal(200.0, follow[^1].StartBeat + follow[^1].DurationBeats);

        for (var i = 0; i < lead.Count; i++)
            Assert.Equal(lead[i].StartBeat, follow[i].StartBeat);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
