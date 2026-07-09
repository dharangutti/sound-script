using Melanchall.DryWetMidi.Core;
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Voice;
using SoundScript.Core.Phonetics;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class SyllabifierTests
{
    [Theory]
    [InlineData("star", "star")]
    [InlineData("twinkle", "twin|kle")]
    [InlineData("little", "lit|tle")]
    [InlineData("shining", "shi|ning")]
    [InlineData("above", "a|bove")]
    [InlineData("wonder", "won|der")]
    [InlineData("diamond", "dia|mond")]
    [InlineData("hello", "hel|lo")]
    [InlineData("music", "mu|sic")]
    [InlineData("table", "ta|ble")]
    [InlineData("shine", "shine")]
    [InlineData("happy", "hap|py")]
    [InlineData("singing", "sin|ging")]
    public void Syllabify_SplitsUsingPhoneticRules(string word, string expected)
    {
        var syllables = Syllabifier.Syllabify(word);
        Assert.Equal(expected, string.Join("|", syllables));
    }

    [Fact]
    public void Syllabify_IsDeterministic()
    {
        var first = Syllabifier.Syllabify("wonderful");
        var second = Syllabifier.Syllabify("wonderful");
        Assert.Equal(first, second);
    }

    [Fact]
    public void Syllabify_PreservesCasing()
    {
        var syllables = Syllabifier.Syllabify("Twinkle");
        Assert.Equal("Twin", syllables[0]);
        Assert.Equal("kle", syllables[1]);
    }

    [Theory]
    [InlineData("star", 1)]
    [InlineData("twinkle", 2)]
    [InlineData("wonderful", 3)]
    public void CountSyllables_MatchesSplit(string word, int expected)
    {
        Assert.Equal(expected, Syllabifier.CountSyllables(word));
    }
}

public class LyricAlignerTests
{
    [Fact]
    public void ToSyllables_MarksWordEnds()
    {
        var syllables = LyricAligner.ToSyllables("Twinkle star");

        Assert.Equal(3, syllables.Count);
        Assert.Equal(("Twin", false), (syllables[0].Text, syllables[0].IsWordEnd));
        Assert.Equal(("kle", true), (syllables[1].Text, syllables[1].IsWordEnd));
        Assert.Equal(("star", true), (syllables[2].Text, syllables[2].IsWordEnd));
    }

    [Fact]
    public void Align_ExtraNotesBecomeMelisma()
    {
        var syllables = LyricAligner.ToSyllables("Ah");
        var slots = LyricAligner.Align(syllables, 3, out var overflowed);

        Assert.False(overflowed);
        Assert.NotNull(slots[0]);
        Assert.Null(slots[1]);
        Assert.Null(slots[2]);
    }

    [Fact]
    public void Align_ExtraSyllablesMergeOntoLastNote()
    {
        var syllables = LyricAligner.ToSyllables("wonderful");
        var slots = LyricAligner.Align(syllables, 2, out var overflowed);

        Assert.True(overflowed);
        Assert.Equal("won", slots[0]!.Value.Text);
        Assert.Equal("derful", slots[1]!.Value.Text);
        Assert.True(slots[1]!.Value.IsWordEnd);
    }
}

public class VoiceParsingTests
{
    [Fact]
    public void Parser_BuildsVoiceNode()
    {
        var program = Parse(
            """
            voice lead {
                vocal choir
                mf
                sing "Twinkle twinkle" C4 q C4 q G4 q G4 q
            }
            """);

        var voice = Assert.Single(program.Statements.OfType<VoiceNode>());
        Assert.Equal("lead", voice.Name);
        Assert.Equal(3, voice.Body.Count);

        var timbre = Assert.Single(voice.Body.OfType<VocalTimbreNode>());
        Assert.Equal(52, timbre.ProgramNumber);

        var sing = Assert.Single(voice.Body.OfType<SingNode>());
        Assert.Equal("Twinkle twinkle", sing.Lyric);
        Assert.Equal(4, sing.Notes.Count);
    }

    [Fact]
    public void Parser_RejectsSingWithoutNotes()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(
            """
            voice lead {
                sing "hello"
            }
            """));
        Assert.Contains("at least one note", ex.Message);
    }

    [Fact]
    public void Parser_RejectsInstrumentInsideVoice()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(
            """
            voice lead {
                instrument piano
            }
            """));
        Assert.Contains("voice body", ex.Message);
    }

    [Fact]
    public void Parser_RejectsUnknownVocalTimbre()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Parse(
            """
            voice lead {
                vocal trombone
            }
            """));
        Assert.Contains("Unknown vocal timbre", ex.Message);
    }

    private static ProgramNode Parse(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
}

public class VocalInterpreterTests
{
    [Fact]
    public void SingleSyllablePerNote_BindsInOrder()
    {
        var interpreted = Interpret(
            """
            voice lead {
                sing "Twinkle star" C4 q C4 q G4 h
            }
            """);

        var track = Assert.Single(interpreted.VocalTracks);
        Assert.Equal("lead", track.Name);
        Assert.Equal(3, track.Syllables.Count);

        Assert.Equal("Twin", track.Syllables[0].Text);
        Assert.Equal(0.0, track.Syllables[0].StartBeat);
        Assert.Equal("kle", track.Syllables[1].Text);
        Assert.Equal(1.0, track.Syllables[1].StartBeat);
        Assert.Equal("star", track.Syllables[2].Text);
        Assert.Equal(2.0, track.Syllables[2].StartBeat);
        Assert.Equal(2.0, track.Syllables[2].DurationBeats);
    }

    [Fact]
    public void Melisma_HoldsVowelAcrossExtraNotes()
    {
        var interpreted = Interpret(
            """
            voice lead {
                sing "Ah" C4 q E4 q G4 q
            }
            """);

        var track = Assert.Single(interpreted.VocalTracks);
        Assert.Equal("Ah", track.Syllables[0].Text);
        Assert.False(track.Syllables[0].IsMelisma);
        Assert.True(track.Syllables[1].IsMelisma);
        Assert.True(track.Syllables[2].IsMelisma);
    }

    [Fact]
    public void Overflow_WarnsAndMergesTail()
    {
        var interpreted = Interpret(
            """
            voice lead {
                sing "wonderful" C4 q C4 q
            }
            """);

        Assert.Contains(interpreted.Warnings, w => w.Contains("syllables"));
        var track = Assert.Single(interpreted.VocalTracks);
        Assert.Equal(2, track.Syllables.Count);
        Assert.Equal("derful", track.Syllables[1].Text);
    }

    [Fact]
    public void RestAndDynamics_ShapeTimeline()
    {
        var interpreted = Interpret(
            """
            voice lead {
                f
                rest q
                sing "star" C4 q
            }
            """);

        var track = Assert.Single(interpreted.VocalTracks);
        var syllable = Assert.Single(track.Syllables);
        Assert.Equal(1.0, syllable.StartBeat);
        Assert.Equal(96, syllable.Velocity);
    }

    [Fact]
    public void VoiceRunsParallel_WithoutDisturbingInstrumentTracks()
    {
        var interpreted = Interpret(
            """
            track melody {
                instrument piano
                C4 q E4 q
            }

            voice lead {
                sing "la la" C5 q C5 q
            }
            """);

        var instrumentTrack = Assert.Single(interpreted.Tracks);
        Assert.Equal(2, instrumentTrack.Notes.Count);

        var vocalTrack = Assert.Single(interpreted.VocalTracks);
        Assert.Equal(2, vocalTrack.Syllables.Count);
        Assert.Equal(0.0, vocalTrack.Syllables[0].StartBeat);
    }

    [Fact]
    public void ScriptWithoutVoice_ProducesNoVocalTracks()
    {
        var interpreted = Interpret("track melody { C4 q }");
        Assert.Empty(interpreted.VocalTracks);
    }

    [Fact]
    public void TempoMap_DrivesSyllableDurations()
    {
        var interpreted = Interpret(
            """
            tempo 60
            voice lead {
                sing "star" C4 q
            }
            """);

        var syllable = Assert.Single(Assert.Single(interpreted.VocalTracks).Syllables);
        Assert.Equal(1000.0, syllable.DurationMs, 3);
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var interpreted = Interpreter.Interpret(program);
        VocalInterpreter.Apply(program, interpreted);
        return interpreted;
    }
}

public class VocalSpeechTimelineTests
{
    [Fact]
    public void Build_ReassemblesSyllablesIntoWords()
    {
        var interpreted = Interpret(
            """
            tempo 120
            voice lead {
                sing "Twinkle little star" C4 q C4 q G4 q G4 q A4 h
            }
            """);

        var words = VocalSpeechTimeline.Build(interpreted);

        Assert.Equal(3, words.Count);
        Assert.Equal("Twinkle", words[0].Text);
        Assert.Equal("little", words[1].Text);
        Assert.Equal("star", words[2].Text);

        // at 120 BPM a quarter note is 500ms; words start at their first syllable
        Assert.Equal(0.0, words[0].StartMs, 3);
        Assert.Equal(1000.0, words[1].StartMs, 3);
        Assert.Equal(2000.0, words[2].StartMs, 3);

        // two quarter-note syllables per word → 1000ms word duration
        Assert.Equal(1000.0, words[0].DurationMs, 3);
        Assert.Equal(60, words[0].Midi);
    }

    [Fact]
    public void Build_MelismaExtendsWordDuration()
    {
        var interpreted = Interpret(
            """
            tempo 120
            voice lead {
                sing "Ah" C4 q E4 q G4 q
            }
            """);

        var word = Assert.Single(VocalSpeechTimeline.Build(interpreted));
        Assert.Equal("Ah", word.Text);
        Assert.Equal(1500.0, word.DurationMs, 3);
    }

    [Fact]
    public void Build_NoVocalTracks_ReturnsEmpty()
    {
        var interpreted = Interpret("track melody { C4 q }");
        Assert.Empty(VocalSpeechTimeline.Build(interpreted));
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var interpreted = Interpreter.Interpret(program);
        VocalInterpreter.Apply(program, interpreted);
        return interpreted;
    }
}

public class VocalMidiExportTests
{
    [Fact]
    public void Export_WritesLyricEventsAndVocalProgram()
    {
        var interpreted = Interpret(
            """
            track melody {
                instrument piano
                C4 q E4 q
            }

            voice lead {
                vocal choir
                sing "Twinkle star" C4 q C4 q G4 h
            }
            """);

        using var stream = new MemoryStream();
        MidiGenerator.Write(interpreted, stream);
        stream.Position = 0;
        var midiFile = MidiFile.Read(stream);

        var events = AllEvents(midiFile);

        var lyrics = events.OfType<LyricEvent>().Select(e => e.Text).ToList();
        Assert.Equal(["Twin", "kle ", "star "], lyrics);

        var programChange = events.OfType<ProgramChangeEvent>().Single(e => (int)e.Channel == 15);
        Assert.Equal(52, (int)programChange.ProgramNumber);

        var trackName = events.OfType<SequenceTrackNameEvent>().Single(e => e.Text == "lead");
        Assert.Equal("lead", trackName.Text);

        var vocalNoteOns = events.OfType<NoteOnEvent>().Where(e => (int)e.Channel == 15).ToList();
        Assert.Equal(3, vocalNoteOns.Count);
    }

    [Fact]
    public void Export_MelismaNotesCarryNoLyricEvent()
    {
        var interpreted = Interpret(
            """
            voice lead {
                sing "Ah" C4 q E4 q G4 q
            }
            """);

        using var stream = new MemoryStream();
        MidiGenerator.Write(interpreted, stream);
        stream.Position = 0;
        var midiFile = MidiFile.Read(stream);

        var events = AllEvents(midiFile);
        Assert.Single(events.OfType<LyricEvent>());
        Assert.Equal(3, events.OfType<NoteOnEvent>().Count());
    }

    [Fact]
    public void Export_IsByteDeterministic()
    {
        const string source =
            """
            voice lead {
                vocal oohs
                sing "shining diamond" C4 q D4 q E4 q F4 q
            }
            """;

        Assert.Equal(Render(source), Render(source));

        static byte[] Render(string script)
        {
            var interpreted = Interpret(script);
            using var stream = new MemoryStream();
            MidiGenerator.Write(interpreted, stream);
            return stream.ToArray();
        }
    }

    private static List<MidiEvent> AllEvents(MidiFile midiFile) =>
        midiFile.GetTrackChunks().SelectMany(chunk => chunk.Events).ToList();

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();
        var interpreted = Interpreter.Interpret(program);
        VocalInterpreter.Apply(program, interpreted);
        return interpreted;
    }
}
