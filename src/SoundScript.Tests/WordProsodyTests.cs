using SoundScript.Compose;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Prosody;
using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

public class WordProsodyPlannerTests
{
    [Fact]
    public void Plan_IsDeterministic()
    {
        // WordProsodyPlan's record-struct equality falls back to reference
        // equality for the Stress list, so compare projections instead of
        // the raw records.
        var words = WordTokenizer.Tokenize("Twinkle twinkle little star");
        var first = WordProsodyPlanner.Plan(words);
        var second = WordProsodyPlanner.Plan(words);

        Assert.Equal(
            first.Select(Describe),
            second.Select(Describe));

        static (int BaseMidi, WordCategory Category, string Stress) Describe(WordProsodyPlan plan) =>
            (plan.BaseMidi, plan.Category, string.Join(",", plan.Stress));
    }

    [Fact]
    public void Plan_ContentWordBandsMatchStartAndEndPosition()
    {
        var plans = WordProsodyPlanner.Plan(WordTokenizer.Tokenize("Hello world"));

        Assert.Equal(WordCategory.Content, plans[0].Category);
        Assert.Equal(64, plans[0].BaseMidi); // Start: C4 + 4
        Assert.Equal(WordCategory.Content, plans[1].Category);
        Assert.Equal(57, plans[1].BaseMidi); // End: C4 - 3
    }

    [Fact]
    public void Plan_ContentWordInMiddleIsCentered()
    {
        var plans = WordProsodyPlanner.Plan(WordTokenizer.Tokenize("Hello brave world"));

        Assert.Equal(60, plans[1].BaseMidi); // Middle: C4
    }

    [Fact]
    public void Plan_FunctionWordIsLowerThanNeighboringContentWords()
    {
        var plans = WordProsodyPlanner.Plan(WordTokenizer.Tokenize("the cat sat"));

        Assert.Equal(WordCategory.Function, plans[0].Category);
        Assert.Equal(53, plans[0].BaseMidi); // Function: C4 - 7, regardless of position
        Assert.True(plans[0].BaseMidi < plans[1].BaseMidi);
    }
}

public class SyllableContourGeneratorTests
{
    [Fact]
    public void GenerateOffsets_PrimaryIsHigherThanUnstressed()
    {
        var offsets = SyllableContourGenerator.GenerateOffsets([StressLevel.Primary, StressLevel.Unstressed]);

        Assert.Equal([2, 0], offsets);
    }

    [Fact]
    public void GenerateOffsets_SecondaryFallsBetweenPrimaryAndUnstressed()
    {
        var offsets = SyllableContourGenerator.GenerateOffsets(
            [StressLevel.Primary, StressLevel.Secondary, StressLevel.Unstressed]);

        Assert.Equal(2, offsets[0]);
        Assert.Equal(1, offsets[1]);
        Assert.Equal(0, offsets[2]);
    }

    [Theory]
    [InlineData(StressLevel.Primary)]
    [InlineData(StressLevel.Secondary)]
    [InlineData(StressLevel.Unstressed)]
    public void GenerateOffsets_AreBoundedWithinThreeSemitones(StressLevel level)
    {
        var offsets = SyllableContourGenerator.GenerateOffsets([level]);

        Assert.InRange(offsets[0], -3, 3);
    }
}

public class PhraseContourEngineTests
{
    [Theory]
    [InlineData("Is this a question?", SentenceType.Question)]
    [InlineData("This is a statement.", SentenceType.Statement)]
    [InlineData("This is a statement", SentenceType.Statement)]
    public void DetectSentenceType_ReadsTrailingPunctuation(string text, SentenceType expected)
    {
        Assert.Equal(expected, PhraseContourEngine.DetectSentenceType(text));
    }

    [Fact]
    public void ComputeDeltas_StatementTrendsDownward()
    {
        var deltas = PhraseContourEngine.ComputeDeltas(4, SentenceType.Statement);

        Assert.Equal([2, 0, -2, -4], deltas);
    }

    [Fact]
    public void ComputeDeltas_QuestionTrendsUpward()
    {
        var deltas = PhraseContourEngine.ComputeDeltas(4, SentenceType.Question);

        Assert.True(deltas[^1] > deltas[0]);
    }

    [Fact]
    public void ComputeDeltas_SingleWordIsNeutral()
    {
        Assert.Equal([0], PhraseContourEngine.ComputeDeltas(1, SentenceType.Statement));
    }
}

public class ProsodyClampTests
{
    [Fact]
    public void Clamp_BoundsAdjacentJumps()
    {
        var clamped = ProsodyClamp.Clamp([60, 90]);

        Assert.InRange(clamped[1] - clamped[0], -5, 5);
    }

    [Fact]
    public void Clamp_BoundsPhraseRange()
    {
        var clamped = ProsodyClamp.Clamp([60, 65, 70, 75, 80, 85, 90]);

        Assert.True(clamped.Max() - clamped.Min() <= 14);
    }

    [Fact]
    public void Clamp_LeavesAnAlreadyInBoundsSequenceUnchanged()
    {
        int[] input = [65, 63, 62, 60, 61, 59, 58];

        Assert.Equal(input, ProsodyClamp.Clamp(input));
    }
}

public class ProsodyComposerTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void BuildAst_ProducesOnePhrasePerSyllableEachWithASinglePitch()
    {
        var program = ProsodyComposer.BuildAst(Example, tempo: 96);

        var tempo = Assert.IsType<TempoNode>(program.Statements[0]);
        Assert.Equal(96, tempo.Bpm);

        var track = Assert.IsType<TrackNode>(program.Statements[1]);
        Assert.Equal("prosody", track.Name);
        Assert.Equal(7, track.Body.Count);

        int[] expectedMidi = [68, 66, 62, 60, 60, 58, 55];
        for (var i = 0; i < track.Body.Count; i++)
        {
            var phrase = Assert.IsType<PhraseNode>(track.Body[i]);
            var notes = phrase.Body.OfType<NoteNode>().ToList();
            Assert.NotEmpty(notes);

            // every phoneme in a syllable shares that syllable's one pitch —
            // phonemes carry timbre/articulation only, not pitch.
            Assert.All(notes, note => Assert.Equal(expectedMidi[i], note.Notation.ToMidiNumber()));
        }
    }

    [Fact]
    public void BuildAst_TotalPitchRangeAndMaxAdjacentJumpAreBounded()
    {
        var program = ProsodyComposer.BuildAst(Example);
        var track = (TrackNode)program.Statements[1];
        var pitches = track.Body.OfType<PhraseNode>()
            .Select(phrase => phrase.Body.OfType<NoteNode>().First().Notation.ToMidiNumber())
            .ToList();

        Assert.True(pitches.Max() - pitches.Min() <= 14);
        for (var i = 1; i < pitches.Count; i++)
            Assert.True(Math.Abs(pitches[i] - pitches[i - 1]) <= 5);
    }

    [Fact]
    public void Compose_ProducesInterpretedTrackWithNotes()
    {
        var track = ProsodyComposer.Compose(Example);

        Assert.Equal("prosody", track.Name);
        Assert.NotEmpty(track.Notes);
    }

    [Fact]
    public void Compose_EmptyTextProducesEmptyTrack()
    {
        var track = ProsodyComposer.Compose("   ");

        Assert.Empty(track.Notes);
    }

    [Fact]
    public void Compose_IdenticalInputProducesIdenticalNotes()
    {
        var first = ProsodyComposer.Compose(Example);
        var second = ProsodyComposer.Compose(Example);

        Assert.Equal(first.Notes, second.Notes);
    }

    [Fact]
    public void ComposeProgram_IdenticalInputProducesIdenticalMidiBytes()
    {
        Assert.Equal(RenderMidi(Example), RenderMidi(Example));
    }

    [Fact]
    public void AppendTo_AddsOneTrackWithoutTouchingExistingOnes()
    {
        var host = new InterpretedProgram();
        var existing = new InterpretedTrack { Name = "piano" };
        existing.Notes.Add(new TimedNote(60, 0, 1, 500));
        host.Tracks.Add(existing);

        ProsodyComposer.AppendTo(host, "star");

        Assert.Equal(2, host.Tracks.Count);
        Assert.Same(existing, host.Tracks[0]);
        Assert.Single(existing.Notes);
        Assert.Equal("prosody", host.Tracks[1].Name);
        Assert.NotEmpty(host.Tracks[1].Notes);
    }

    private static byte[] RenderMidi(string text)
    {
        var program = ProsodyComposer.ComposeProgram(text);
        using var stream = new MemoryStream();
        MidiGenerator.Write(program, stream);
        return stream.ToArray();
    }
}

public class ProsodyRenderTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void RenderToWavBytes_ProducesValidWavHeaderFromProsodyMidi()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(ProsodyComposer.ComposeProgram(Example), stream);

        var wav = OfflineRenderer.RenderToWavBytes(
            stream.ToArray(),
            options: new OfflineRenderer.RenderOptions { SourceText = Example, PreferredTrackName = "prosody" });

        Assert.Equal("RIFF"u8.ToArray(), wav.AsSpan(0, 4).ToArray());
        Assert.True(wav.Length > 44); // header plus actual sample data
    }

    [Fact]
    public void Timeline_AlignsNotesFromTheProsodyTrackByName()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(ProsodyComposer.ComposeProgram(Example), stream);
        var temp = Path.Combine(Path.GetTempPath(), $"ss-prosody-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, stream.ToArray());

        try
        {
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: PhonemeTimbreMapper.PhonemesFromText(Example),
                preferredTrackName: "prosody");

            Assert.NotEmpty(timeline.Frames);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Timeline_PreferredTrackNameSelectsProsodyTrackAmongMultipleNamedTracks()
    {
        // Reproduces the multi-track shape docs/word-prosody.md describes for
        // `--append`: a PhonemeComposer ("phonemes") track alongside a
        // ProsodyComposer ("prosody") track in the same file. Before
        // MidiGenerator wrote a SequenceTrackNameEvent for ordinary tracks,
        // PreferredTrackName had no track names to match against, so it only
        // "worked" by always picking whichever track chunk came first —
        // invisible with the single-track files the other tests here use.
        const string PhonemeText = "hello";
        const string ProsodyText = "star";

        var program = PhonemeComposer.ComposeProgram(PhonemeText);
        ProsodyComposer.AppendTo(program, ProsodyText);

        using var stream = new MemoryStream();
        MidiGenerator.Write(program, stream);
        var temp = Path.Combine(Path.GetTempPath(), $"ss-prosody-multi-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, stream.ToArray());

        try
        {
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: PhonemeTimbreMapper.PhonemesFromText(ProsodyText),
                preferredTrackName: "prosody");

            var expectedNoteCount = ProsodyComposer.Compose(ProsodyText).Notes.Count;
            var phonemeTrackNoteCount = PhonemeComposer.Compose(PhonemeText).Notes.Count;

            Assert.NotEqual(phonemeTrackNoteCount, expectedNoteCount);
            Assert.Equal(expectedNoteCount, timeline.Segments.Count);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void RenderSha256_IsStableAcrossRuns()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(ProsodyComposer.ComposeProgram(Example), stream);
        var midiBytes = stream.ToArray();
        var options = new OfflineRenderer.RenderOptions { SourceText = Example, PreferredTrackName = "prosody" };

        var hashA = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);
        var hashB = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }
}
