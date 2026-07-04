using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class PhraseV3Tests
{
    [Theory]
    [InlineData("curve gentle", PhraseCurveType.Soft)]
    [InlineData("curve soft", PhraseCurveType.Soft)]
    [InlineData("curve strong", PhraseCurveType.Hard)]
    [InlineData("curve aggressive", PhraseCurveType.Hard)]
    [InlineData("curve hard", PhraseCurveType.Hard)]
    [InlineData("curve expressive", PhraseCurveType.Expressive)]
    [InlineData("curve swell", PhraseCurveType.Swell)]
    [InlineData("curve fade", PhraseCurveType.Fade)]
    public void Parse_PhraseCurveAliases(string curveStatement, PhraseCurveType expected)
    {
        var source = $$"""
            track melody {
                phrase {
                    {{curveStatement}}
                    mf
                    C4 q
                }
            }
            """;

        var program = Parse(source);
        var phrase = GetPhrase(program);
        var curve = Assert.IsType<PhraseCurveNode>(phrase.Body[0]);
        Assert.Equal(expected, curve.Curve);
    }

    [Theory]
    [InlineData("transition sharp", PhraseTransitionMode.Abrupt)]
    [InlineData("transition abrupt", PhraseTransitionMode.Abrupt)]
    [InlineData("transition soft", PhraseTransitionMode.Soft)]
    [InlineData("transition expressive", PhraseTransitionMode.Expressive)]
    public void Parse_PhraseTransitionAliases(string transitionStatement, PhraseTransitionMode expected)
    {
        var source = $$"""
            track melody {
                phrase {
                    {{transitionStatement}}
                    mf
                    C4 q
                }
            }
            """;

        var program = Parse(source);
        var phrase = GetPhrase(program);
        var transition = Assert.IsType<PhraseTransitionNode>(phrase.Body[0]);
        Assert.Equal(expected, transition.Mode);
    }

    [Fact]
    public void Parse_PhraseArticulationDefault()
    {
        const string source = """
            track melody {
                phrase {
                    articulation legato
                    mf
                    C4 q
                }
            }
            """;

        var phrase = GetPhrase(Parse(source));
        var articulation = Assert.IsType<PhraseArticulationNode>(phrase.Body[0]);
        Assert.Equal(ArticulationType.Legato, articulation.Articulation);
    }

    [Fact]
    public void Parse_PhraseEnvelopeAndTiming()
    {
        const string source = """
            track melody {
                phrase {
                    crescendo
                    swing 0.67
                    push 0.02
                    pull 0.01
                    mf
                    C4 q E4 q
                }
            }
            """;

        var phrase = GetPhrase(Parse(source));
        Assert.IsType<PhraseEnvelopeNode>(phrase.Body[0]);
        Assert.IsType<PhraseSwingNode>(phrase.Body[1]);
        Assert.IsType<PhrasePushNode>(phrase.Body[2]);
        Assert.IsType<PhrasePullNode>(phrase.Body[3]);
    }

    [Theory]
    [InlineData("curve gentle", "curve soft")]
    [InlineData("curve aggressive", "curve hard")]
    [InlineData("curve strong", "curve hard")]
    [InlineData("transition sharp", "transition abrupt")]
    public void Interpret_AliasesMatchCanonicalOutput(string aliasPhrase, string canonicalPhrase)
    {
        var aliasNotes = NotesForPhrase(aliasPhrase);
        var canonicalNotes = NotesForPhrase(canonicalPhrase);
        Assert.Equal(SerializeNotes(canonicalNotes), SerializeNotes(aliasNotes));
    }

    [Fact]
    public void Interpret_V2PhraseExampleRemainsUnchanged()
    {
        const string source = """
            tempo 108
            instrument violin

            track melody {
                phrase {
                    curve soft
                    transition smooth
                    mf
                    C4 q
                    E4 q
                    G4 q
                }
                phrase {
                    transition abrupt
                    f
                    C5 q
                    G4 q
                    E4 q
                }
            }
            """;

        var notes = Interpret(source).Tracks.Single().Notes;
        Assert.Equal(
            """
            60,0.000000000,1.000000000,102,0
            64,1.000000000,1.000000000,110,0
            67,2.000000000,1.000000000,102,0
            72,3.000000000,1.000000000,115,0
            67,4.000000000,1.000000000,115,0
            64,5.000000000,1.000000000,115,0
            """,
            SerializeNotes(notes));
    }

    [Fact]
    public void Interpret_SwellCurveIncreasesVelocityAcrossPhrase()
    {
        const string source = """
            track melody {
                phrase {
                    curve swell
                    mf
                    C4 q E4 q G4 q
                }
            }
            """;

        var velocities = Interpret(source).Tracks.Single().Notes.Select(note => note.Velocity).ToArray();
        Assert.True(velocities[1] > velocities[0]);
        Assert.True(velocities[2] > velocities[1]);
    }

    [Fact]
    public void Interpret_CrescendoIncreasesVelocityAcrossPhrase()
    {
        const string source = """
            track melody {
                phrase {
                    crescendo
                    mf
                    C4 q E4 q G4 q
                }
            }
            """;

        var velocities = Interpret(source).Tracks.Single().Notes.Select(note => note.Velocity).ToArray();
        Assert.True(velocities[1] > velocities[0]);
        Assert.True(velocities[2] > velocities[1]);
    }

    [Fact]
    public void Interpret_PhraseArticulationAppliesToAllNotes()
    {
        const string source = """
            track melody {
                phrase {
                    articulation staccato
                    mf
                    C4 q E4 q
                }
            }
            """;

        var durations = Interpret(source).Tracks.Single().Notes.Select(note => note.DurationBeats).ToArray();
        Assert.All(durations, duration => Assert.Equal(0.47, duration));
    }

    [Fact]
    public void Interpret_SwingDelaysOffbeatNotes()
    {
        const string source = """
            track melody {
                phrase {
                    swing 0.67
                    mf
                    C4 e E4 e G4 e
                }
            }
            """;

        var starts = Interpret(source).Tracks.Single().Notes.Select(note => note.StartBeat).ToArray();
        Assert.Equal(0.0, starts[0]);
        Assert.True(starts[1] > 0.5);
        Assert.Equal(1.0, starts[2]);
    }

    [Fact]
    public void Interpret_PushPullAdjustStartBeat()
    {
        const string source = """
            track melody {
                rest q
                phrase {
                    push 0.05
                    mf
                    C4 q
                }
            }
            """;

        var pushed = Interpret(source).Tracks.Single().Notes.Single(note => note.MidiNumber == 60).StartBeat;

        const string baseline = """
            track melody {
                rest q
                phrase {
                    mf
                    C4 q
                }
            }
            """;

        var baselineStart = Interpret(baseline).Tracks.Single().Notes.Single(note => note.MidiNumber == 60).StartBeat;
        Assert.Equal(baselineStart - 0.05, pushed, 9);
    }

    [Theory]
    [InlineData("examples/phrases.ss")]
    [InlineData("examples/phrase-smoothing.ss")]
    [InlineData("examples/articulations.ss")]
    [InlineData("examples/dynamics.ss")]
    public void Golden_V2Examples_ProduceStableNoteOutput(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../", relativePath));
        var source = File.ReadAllText(path);
        var snapshotPath = ResolveGoldenPath(Path.GetFileName(relativePath) + ".notes.txt");

        var actual = SerializeAllTracks(Interpret(source));

        if (!File.Exists(snapshotPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllText(snapshotPath, actual);
        }

        var expected = File.ReadAllText(snapshotPath);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Golden_V2Examples_MidiBytesRemainStable()
    {
        const string source = """
            tempo 108
            track melody {
                phrase {
                    curve soft
                    transition smooth
                    mf
                    C4 q E4 q G4 q
                }
            }
            """;

        var interpreted = Interpret(source);
        using var stream = new MemoryStream();
        MidiGenerator.Write(interpreted, stream);
        var actualBytes = stream.ToArray();
        var goldenPath = ResolveGoldenPath("phrases-v2-baseline.mid");

        if (!File.Exists(goldenPath))
            File.WriteAllBytes(goldenPath, actualBytes);

        Assert.Equal(File.ReadAllBytes(goldenPath), actualBytes);
    }

    private static string ResolveGoldenPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Golden", fileName);

    private static IReadOnlyList<TimedNote> NotesForPhrase(string innerPhrase) =>
        Interpret($$"""
            track melody {
                phrase {
                    {{innerPhrase}}
                    mf
                    C4 q E4 q G4 q
                }
            }
            """).Tracks.Single().Notes;

    private static PhraseNode GetPhrase(ProgramNode program)
    {
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        return Assert.IsType<PhraseNode>(track.Body[0]);
    }

    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static InterpretedProgram Interpret(string source) =>
        Interpreter.Interpret(Parse(source));

    private static string SerializeNotes(IReadOnlyList<TimedNote> notes) =>
        string.Join('\n', notes.Select(note =>
            $"{note.MidiNumber},{note.StartBeat:F9},{note.DurationBeats:F9},{note.Velocity},{note.Channel}"));

    private static string SerializeAllTracks(InterpretedProgram program) =>
        string.Join('\n', program.Tracks.Select(track =>
            $"[{track.Name}]\n{SerializeNotes(track.Notes)}"));
}
