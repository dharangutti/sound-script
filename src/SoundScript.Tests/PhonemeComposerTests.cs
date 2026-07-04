using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;

namespace SoundScript.Tests;

public class PhonemeSplitterTests
{
    [Theory]
    [InlineData("star", "s|t|aa|r")]
    [InlineData("twin", "t|w|ai|n")]
    [InlineData("kle", "k|l|ee")]
    [InlineData("shine", "sh|ai|n|ee")]
    [InlineData("chat", "ch|aa|t")]
    [InlineData("sing", "s|ai|ng")]
    [InlineData("queen", "k|w|ee|n")]
    [InlineData("phase", "f|aa|s|ee")]
    [InlineData("moon", "m|oo|n")]
    [InlineData("out", "au|t")]
    public void Split_UsesDigraphAndVowelRules(string syllable, string expected)
    {
        Assert.Equal(expected, string.Join("|", PhonemeSplitter.Split(syllable)));
    }

    [Fact]
    public void Split_CollapsesDoubledConsonants()
    {
        Assert.Equal("h|ai|s", string.Join("|", PhonemeSplitter.Split("hiss")));
    }

    [Fact]
    public void Split_IgnoresCaseAndNonLetters()
    {
        Assert.Equal(PhonemeSplitter.Split("Star!"), PhonemeSplitter.Split("star"));
    }

    [Fact]
    public void Split_EmptyInputYieldsNoPhonemes()
    {
        Assert.Empty(PhonemeSplitter.Split(""));
    }
}

public class PhonemeMapperTests
{
    [Theory]
    [InlineData("p", GestureKind.Staccato, PitchClass.C, 3, NoteDuration.Eighth)]
    [InlineData("t", GestureKind.Staccato, PitchClass.C, 4, NoteDuration.Eighth)]
    [InlineData("k", GestureKind.Staccato, PitchClass.C, 4, NoteDuration.Eighth)]
    [InlineData("m", GestureKind.Swell, PitchClass.C, 3, NoteDuration.Quarter)]
    [InlineData("n", GestureKind.Swell, PitchClass.C, 3, NoteDuration.Quarter)]
    [InlineData("s", GestureKind.Fade, PitchClass.C, 5, NoteDuration.Eighth)]
    [InlineData("sh", GestureKind.Fade, PitchClass.C, 5, NoteDuration.Eighth)]
    [InlineData("r", GestureKind.Accent, PitchClass.G, 4, NoteDuration.Eighth)]
    [InlineData("l", GestureKind.Accent, PitchClass.E, 4, NoteDuration.Eighth)]
    [InlineData("aa", GestureKind.Legato, PitchClass.C, 4, NoteDuration.Quarter)]
    [InlineData("ee", GestureKind.Legato, PitchClass.E, 4, NoteDuration.Quarter)]
    [InlineData("oo", GestureKind.Legato, PitchClass.G, 3, NoteDuration.Quarter)]
    [InlineData("ai", GestureKind.Legato, PitchClass.D, 4, NoteDuration.Quarter)]
    [InlineData("au", GestureKind.Legato, PitchClass.F, 4, NoteDuration.Quarter)]
    public void Map_MatchesSpecificationTable(
        string phoneme, GestureKind kind, PitchClass pitch, int octave, NoteDuration duration)
    {
        Assert.Equal(new MusicalGesture(kind, pitch, octave, duration), PhonemeMapper.Map(phoneme));
    }

    [Fact]
    public void Map_UnknownPhonemeFallsBackToDefault()
    {
        Assert.Equal(PhonemeMapper.DefaultGesture, PhonemeMapper.Map("zz"));
        Assert.False(PhonemeMapper.TryMap("zz", out _));
    }
}

public class PhonemeComposerTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void SplitSyllables_UsesExistingSyllabifier()
    {
        var syllables = PhonemeComposer.SplitSyllables(Example);
        Assert.Equal("Twin|kle|twin|kle|lit|tle|star", string.Join("|", syllables));
    }

    [Fact]
    public void BuildAst_ProducesOnePhrasePerSyllable()
    {
        var program = PhonemeComposer.BuildAst(Example, tempo: 96);

        var tempo = Assert.IsType<TempoNode>(program.Statements[0]);
        Assert.Equal(96, tempo.Bpm);

        var track = Assert.IsType<TrackNode>(program.Statements[1]);
        Assert.Equal(PhraseAssembler.TrackName, track.Name);
        Assert.Equal(7, track.Body.Count);
        Assert.All(track.Body, node => Assert.IsType<PhraseNode>(node));
    }

    [Fact]
    public void Compose_ProducesInterpretedTrackWithNotes()
    {
        var track = PhonemeComposer.Compose(Example);

        Assert.Equal(PhraseAssembler.TrackName, track.Name);
        Assert.NotEmpty(track.Notes);
    }

    [Fact]
    public void Compose_EmptyTextProducesEmptyTrack()
    {
        var track = PhonemeComposer.Compose("   ");
        Assert.Empty(track.Notes);
    }

    [Fact]
    public void Compose_IdenticalInputProducesIdenticalNotes()
    {
        var first = PhonemeComposer.Compose(Example);
        var second = PhonemeComposer.Compose(Example);

        Assert.Equal(first.Notes, second.Notes);
    }

    [Fact]
    public void ComposeProgram_IdenticalInputProducesIdenticalMidiBytes()
    {
        Assert.Equal(RenderMidi(Example), RenderMidi(Example));
    }

    [Fact]
    public void ComposeProgram_DifferentTextProducesDifferentMidi()
    {
        Assert.NotEqual(RenderMidi("Twinkle star"), RenderMidi("Little diamond"));
    }

    [Fact]
    public void AppendTo_AddsOneTrackWithoutTouchingExistingOnes()
    {
        var host = new InterpretedProgram();
        var existing = new InterpretedTrack { Name = "piano" };
        existing.Notes.Add(new TimedNote(60, 0, 1, 500));
        host.Tracks.Add(existing);

        PhonemeComposer.AppendTo(host, "star");

        Assert.Equal(2, host.Tracks.Count);
        Assert.Same(existing, host.Tracks[0]);
        Assert.Single(existing.Notes);
        Assert.Equal(PhraseAssembler.TrackName, host.Tracks[1].Name);
        Assert.NotEmpty(host.Tracks[1].Notes);
    }

    [Fact]
    public void GestureBuilder_SetsArticulationsAndEnvelopes()
    {
        var staccato = GestureBuilder.BuildNote(PhonemeMapper.Map("t"));
        Assert.Equal(ArticulationType.Staccato, staccato.Notation.Articulation);
        Assert.Null(staccato.Velocity);

        var swell = GestureBuilder.BuildNote(PhonemeMapper.Map("m"));
        Assert.Null(swell.Notation.Articulation);
        Assert.NotNull(swell.Velocity);

        var crescendo = GestureBuilder.BuildEnvelope(GestureKind.Swell);
        Assert.Equal(PhraseEnvelopeType.Crescendo, crescendo!.Envelope);

        var decrescendo = GestureBuilder.BuildEnvelope(GestureKind.Fade);
        Assert.Equal(PhraseEnvelopeType.Decrescendo, decrescendo!.Envelope);

        Assert.Null(GestureBuilder.BuildEnvelope(GestureKind.Legato));
    }

    private static byte[] RenderMidi(string text)
    {
        var program = PhonemeComposer.ComposeProgram(text);
        using var stream = new MemoryStream();
        MidiGenerator.Write(program, stream);
        return stream.ToArray();
    }
}
