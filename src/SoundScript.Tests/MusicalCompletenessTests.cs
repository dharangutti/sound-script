using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using SoundScript.Parser;
using Xunit;

namespace SoundScript.Tests;

public sealed class MusicalCompletenessTests
{
    private static ProgramNode Parse(string source) => new SoundScript.Parser.Parser(new Tokenizer(source).Tokenize()).Parse();

    [Theory]
    [InlineData("C-1", 0)]
    [InlineData("G9", 127)]
    public void PitchNotation_CoversCompleteMidiBoundary(string text, int midi)
    {
        var note = Assert.Single(Assert.Single(Parse($"melody {{ {text} }}").Statements.OfType<MelodyNode>()).Body.OfType<NoteNode>());
        Assert.Equal(midi, note.ToMidiNumber());
    }

    [Fact]
    public void PitchNotation_RejectsAboveMidi127()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse("melody { G#9 }"));
        Assert.Contains("outside the supported range 0-127", ex.Message);
    }

    [Theory]
    [InlineData("Cmaj9", ChordQuality.Major9, 5)]
    [InlineData("Cm9", ChordQuality.Minor9, 5)]
    [InlineData("C9", ChordQuality.Dominant9, 5)]
    [InlineData("Csus4", ChordQuality.Sus4, 3)]
    [InlineData("Cdim7", ChordQuality.Diminished7, 4)]
    [InlineData("Cadd9", ChordQuality.Add9, 4)]
    public void ExtendedChordQualities_ProduceExpectedVoices(string token, ChordQuality quality, int voiceCount)
    {
        var chord = Assert.Single(Assert.Single(Parse($"melody {{ {token} q }}").Statements.OfType<MelodyNode>()).Body.OfType<ChordNode>());
        Assert.Equal(quality, chord.Quality);
        Assert.Equal(voiceCount, chord.ToMidiNumbers().Count);
    }

    [Fact]
    public void RhythmicModifiers_ResolveToDeterministicBeatValues()
    {
        var melody = Assert.Single(Parse("melody { C4 q. triplet e D4 tuplet 5 in 4 e E4 grace e F4 }").Statements.OfType<MelodyNode>());
        var notes = melody.Body.OfType<NoteNode>().ToArray();
        Assert.Equal(1.5, notes[0].DurationBeats);
        Assert.Equal(1.0 / 3.0, notes[1].DurationBeats, 6);
        Assert.Equal(0.4, notes[2].DurationBeats, 6);
        Assert.Equal(0.125, notes[3].DurationBeats, 6);
    }

    [Theory]
    [InlineData("piano", 0)]
    [InlineData("violin", 40)]
    [InlineData("lead_square", 80)]
    [InlineData("127", 127)]
    public void InstrumentMap_ResolvesLegacyNamesAllProgramsAndNumericBoundary(string name, int program)
    {
        Assert.Equal(program, InstrumentMap.Resolve(name));
        Assert.True(InstrumentMap.TryGetName(program, out _));
    }

    [Theory]
    [InlineData("ppp", DynamicLevel.Pianissimo, 32)]
    [InlineData("fff", DynamicLevel.Fortissimo, 112)]
    [InlineData("sfz", DynamicLevel.Sforzando, 120)]
    public void ExtendedDynamics_MapToConventionalVelocities(string marking, DynamicLevel level, int velocity)
    {
        var dynamic = Assert.Single(Assert.Single(Parse($"melody {{ {marking} }}").Statements.OfType<MelodyNode>()).Body.OfType<DynamicNode>());
        Assert.Equal(level, dynamic.Level);
        Assert.Equal(velocity, level.ToVelocity());
    }
}
