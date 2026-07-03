using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class PhraseShapingTests
{
    [Fact]
    public void Parse_PhraseBlockWithDynamics()
    {
        const string source = """
            track melody {
                phrase {
                    mf
                    C4 q E4 q G4 q
                }
            }
            """;

        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var track = Assert.IsType<TrackNode>(program.Statements[0]);
        var phrase = Assert.IsType<PhraseNode>(track.Body[0]);

        Assert.IsType<DynamicNode>(phrase.Body[0]);
        Assert.Equal(3, phrase.Body.Count(node => node is NoteNode));
    }

    [Fact]
    public void Interpret_AppliesPhraseDynamicsBeforePlaybackShaping()
    {
        const string source = """
            track melody {
                phrase {
                    mf
                    C4 q
                }
            }
            """;

        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();
        var expected = PlaybackShaper.ShapeNote(
            PhraseShaper.Apply(DynamicLevel.MezzoForte.ToVelocity(), new PhraseScope { NoteCount = 1 }).Velocity,
            null,
            null,
            DynamicLevel.MezzoForte,
            64,
            null,
            null,
            1.0).Velocity;

        Assert.Equal(expected, note.Velocity);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Phrase shaping applied"));
    }

    [Fact]
    public void Interpret_AppliesPhraseCurve()
    {
        const string source = """
            track melody {
                phrase {
                    curve soft
                    mf
                    C4 q
                }
            }
            """;

        var scope = new PhraseScope { Curve = PhraseCurveType.Soft, NoteCount = 1 };
        var phraseVelocity = PhraseShaper.Apply(DynamicLevel.MezzoForte.ToVelocity(), scope).Velocity;
        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();
        var expected = PlaybackShaper.ShapeNote(
            phraseVelocity,
            null,
            null,
            DynamicLevel.MezzoForte,
            64,
            null,
            null,
            1.0).Velocity;

        Assert.Equal(expected, note.Velocity);
    }

    [Fact]
    public void Interpret_AppliesSmoothPhraseTransitions()
    {
        const string source = """
            track melody {
                phrase {
                    transition smooth
                    mf
                    C4 q E4 q G4 q
                }
            }
            """;

        var interpreted = Interpret(source);
        var velocities = interpreted.Tracks.Single().Notes.Select(note => note.Velocity).ToArray();
        var scope = new PhraseScope
        {
            Transition = PhraseTransitionMode.Smooth,
            NoteCount = 3
        };

        int[] expected =
        [
            PlaybackShaper.ShapeNote(PhraseShaper.Apply(DynamicLevel.MezzoForte.ToVelocity(), scope).Velocity, null, null, DynamicLevel.MezzoForte, 64, null, null, 1.0).Velocity,
            PlaybackShaper.ShapeNote(PhraseShaper.Apply(DynamicLevel.MezzoForte.ToVelocity(), scope).Velocity, null, null, DynamicLevel.MezzoForte, 64, null, null, 1.0).Velocity,
            PlaybackShaper.ShapeNote(PhraseShaper.Apply(DynamicLevel.MezzoForte.ToVelocity(), scope).Velocity, null, null, DynamicLevel.MezzoForte, 64, null, null, 1.0).Velocity
        ];

        Assert.Equal(expected, velocities);
        Assert.True(velocities[1] > velocities[0]);
        Assert.True(velocities[1] > velocities[2]);
    }

    [Fact]
    public void Interpret_AbruptTransitionSkipsEnvelope()
    {
        const string source = """
            track melody {
                phrase {
                    transition abrupt
                    mf
                    C4 q E4 q
                }
            }
            """;

        var interpreted = Interpret(source);
        var velocities = interpreted.Tracks.Single().Notes.Select(note => note.Velocity).ToArray();

        Assert.Equal(velocities[0], velocities[1]);
    }

    [Fact]
    public void Interpret_MultiPhraseScriptPreservesBoundaries()
    {
        const string source = """
            track melody {
                phrase {
                    mf
                    C6 q
                }
                phrase {
                    mf
                    C4 q
                }
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(84, notes[0].MidiNumber);
        Assert.Equal(72, notes[1].MidiNumber);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Phrase smoothing applied"));
    }

    [Fact]
    public void Interpret_PreservesBlockBoundariesWithPhrases()
    {
        const string source = """
            block motif {
                C6 q
            }
            track melody {
                phrase {
                    mf
                    play motif
                }
                C4 q
            }
            """;

        var interpreted = Interpret(source);
        var notes = interpreted.Tracks.Single().Notes;

        Assert.Equal(84, notes[0].MidiNumber);
        Assert.Equal(72, notes[1].MidiNumber);
        Assert.Contains(interpreted.Warnings, warning => warning.Contains("Phrase smoothing applied"));
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
