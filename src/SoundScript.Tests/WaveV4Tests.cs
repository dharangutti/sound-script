// UNDER DEVELOPMENT — v4
// Verification for the SoundScript.Wave multi-part audibility fixes: phrase{}
// blocks are entered (Bug 1), voice { sing ... } lines render on their
// explicit pitches (Bug 2), and 'play <pattern> <chord> <duration>' strums a
// chord instead of aborting the whole render (Bug 3). WaveRenderingTests (v1),
// WaveV2Tests, and WaveV3Tests are intentionally untouched; their passing
// unmodified proves the pre-v4 paths survived.
using System.Linq;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Mixing;
using SoundScript.Wave.Model;
using SoundScript.Wave.Prosody;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveV4Tests
{
    private const int SampleRate = 44_100;

    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    private static double MidiToHz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

    private static double FullRms(float[] samples)
    {
        if (samples.Length == 0)
            return 0.0;

        var sum = 0.0;
        foreach (var s in samples)
            sum += (double)s * s;

        return Math.Sqrt(sum / samples.Length);
    }

    // ---- Bug 1: phrase{} blocks are entered, not skipped ----

    [Fact]
    public void Phrase_WrappedNotes_Render()
    {
        const string source = """
            track melody {
                phrase {
                    C4 q
                    E4 q
                    G4 q
                }
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["melody"];
        Assert.Equal(3, notes.Count);
    }

    [Fact]
    public void Phrase_ProducesSameNotesAsUnwrapped()
    {
        // Entering a phrase must be behaviourally identical to inlining its
        // notes — the shaping directives inside are the only deferred part.
        var wrapped = AstToNoteEventAdapter.Convert(ParseSsw("""
            track melody {
                phrase {
                    curve soft
                    transition smooth
                    C4 q
                    E4 q
                    G4 h
                }
            }
            """))["melody"];

        var inline = AstToNoteEventAdapter.Convert(ParseSsw("""
            track melody {
                C4 q
                E4 q
                G4 h
            }
            """))["melody"];

        Assert.Equal(inline.Count, wrapped.Count);
        for (var i = 0; i < inline.Count; i++)
        {
            Assert.Equal(inline[i].FrequencyHz, wrapped[i].FrequencyHz, 6);
            Assert.Equal(inline[i].StartTimeSeconds, wrapped[i].StartTimeSeconds, 6);
            Assert.Equal(inline[i].DurationSeconds, wrapped[i].DurationSeconds, 6);
        }
    }

    [Fact]
    public void Phrase_PlayInsidePhrase_ResolvesBlock()
    {
        const string source = """
            block hook {
                C4 q
                E4 q
            }
            track melody {
                phrase {
                    transition smooth
                    play hook
                }
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["melody"];
        Assert.Equal(2, notes.Count);
    }

    // ---- Bug 2: voice { sing ... } renders on explicit pitches ----

    [Fact]
    public void Sing_RendersAudibleNotesForEachSyllable()
    {
        const string source = """
            voice lead {
                vocal choir
                mf
                sing "Jingle bells" E4 q E4 q E4 h
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        Assert.True(tracks.ContainsKey("lead"), "voice block did not create its track");

        var notes = tracks["lead"];
        Assert.NotEmpty(notes);

        // "Jingle bells" → Jin·gle·bells = 3 syllables aligned 1:1 with 3 notes.
        // Every sung syllable here leads on a vowel nucleus, so each renders a
        // sustained tone (fundamental + formant overtone); the render carries
        // real energy rather than being technically-present-but-silent.
        var buffer = Mixer.RenderTrack(notes, SampleRate);
        Assert.True(FullRms(buffer) > 0.05, $"Sung line is near-silent: RMS {FullRms(buffer):F5}");
    }

    [Fact]
    public void Sing_BindsToTheNotesExplicitPitch()
    {
        // The one real difference from `speak`: pitch comes from the note, not
        // a generated frequency. A sung E4 must put a tone at E4's fundamental.
        const string source = """
            voice lead {
                sing "la" E4 q
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["lead"];
        var e4 = MidiToHz(64); // E4

        Assert.Contains(notes, n => Math.Abs(n.FrequencyHz - e4) < 0.01);
    }

    [Fact]
    public void Sing_SyllableNoteMismatch_AlignsFirstMinAndStillRenders()
    {
        // Two syllables ("hel·lo"), three notes → align the first two 1:1,
        // drop the surplus note; the line still renders (no crash, no silence).
        const string source = """
            voice lead {
                sing "hello" C4 q E4 q G4 q
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["lead"];
        Assert.NotEmpty(notes);

        // Beat cursor only advanced for the two aligned notes (2 quarters), so
        // no rendered event starts at/after the third note's beat (0.5s @ 120).
        var latestStart = notes.Max(n => n.StartTimeSeconds);
        Assert.True(latestStart < 1.0, $"Dropped note appears to have advanced the cursor: {latestStart}");
    }

    [Fact]
    public void Sing_IsDeterministicAcrossRuns()
    {
        const string source = """
            voice lead {
                vocal choir
                sing "Jingle bells jingle bells" E4 q E4 q E4 h E4 q E4 q E4 h
            }
            """;

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("star", 1)]
    [InlineData("twinkle", 2)]
    [InlineData("little", 2)]
    [InlineData("wonder", 2)]
    [InlineData("hello", 2)]
    [InlineData("jingle", 2)]
    [InlineData("Jingle bells jingle bells", 6)]
    public void SyllableSplitter_CountsMatchTheCommonCase(string lyric, int expectedCount)
    {
        Assert.Equal(expectedCount, SyllableSplitter.Split(lyric).Count);
    }

    [Fact]
    public void SyllableSplitter_IsDeterministic()
    {
        Assert.Equal(
            SyllableSplitter.Split("Jingle all the way"),
            SyllableSplitter.Split("Jingle all the way"));
    }

    // ---- Bug 3: play <pattern> <chord> <duration> strums instead of throwing ----

    [Fact]
    public void PlayPatternChord_StrumsInsteadOfThrowing()
    {
        const string source = """
            pattern strumPat {
                strum
            }
            track guitar {
                play strumPat Cmaj w
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["guitar"];

        // Cmaj = 3 tones, each its own NoteEvent.
        Assert.Equal(3, notes.Count);

        // A strum staggers the tones — their onsets are strictly increasing,
        // not all stacked at the same instant like a block chord.
        var starts = notes.Select(n => n.StartTimeSeconds).ToList();
        for (var i = 1; i < starts.Count; i++)
            Assert.True(starts[i] > starts[i - 1], "Strum tones are not staggered in time");
    }

    [Fact]
    public void PlayPatternChord_DownDirection_LeadsWithTheTopTone()
    {
        // A pattern parses to Direction.Up by default; build the Down case via
        // the AST directly since the grammar has no "strum down" surface yet.
        var pattern = new PatternNode { Name = "d", Direction = PatternDirection.Down };
        var context = ParseSsw("pattern d { down }"); // ensure "down" parses at all
        Assert.IsType<PatternNode>(context.Statements[0]);

        var program = new ProgramNode();
        program.Statements.Add(pattern);
        program.Statements.Add(new TrackNode { Name = "g" });
        var track = (TrackNode)program.Statements[1];
        track.Body.Add(new PlayNode
        {
            SequenceName = "d",
            PatternChord = new ChordNode { Root = 'C', Quality = ChordQuality.Major, Octave = 4, DurationBeats = 4.0 },
        });

        var notes = AstToNoteEventAdapter.Convert(program)["g"];
        Assert.Equal(3, notes.Count);

        // Down strum: the earliest-onset tone is the highest pitch (G4), the
        // latest is the lowest (C4).
        var byOnset = notes.OrderBy(n => n.StartTimeSeconds).ToList();
        Assert.True(byOnset.First().FrequencyHz > byOnset.Last().FrequencyHz,
            "Down strum did not lead with the top tone");
    }

    [Fact]
    public void PlayPattern_WithoutChord_StillThrowsClearly()
    {
        const string source = """
            pattern arp {
                up
            }
            track lead {
                play arp
            }
            """;

        var ex = Assert.Throws<NotSupportedException>(
            () => AstToNoteEventAdapter.Convert(ParseSsw(source)));

        Assert.Contains("without an inline chord", ex.Message);
        Assert.Contains("strum-a-chord", ex.Message);
    }

    [Fact]
    public void PlayPatternChord_DoesNotAbortTheWholeRender()
    {
        // Regression: the pattern throw used to propagate through WaveRenderer
        // and abort every track, not just its own — a whole silent file.
        const string source = """
            pattern strumPat {
                strum
            }
            track melody {
                C4 q E4 q G4 q
            }
            track chords {
                play strumPat Cmaj w
            }
            """;

        var bytes = WaveRenderer.RenderToBytes(ParseSsw(source));
        Assert.NotEmpty(bytes);
    }

    // ---- All three together: the full four-part song ----

    [Fact]
    public void FullSong_AllFourPartsAreAudible()
    {
        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(FourPartSong));

        foreach (var name in new[] { "melody", "harmony", "bass", "choirline" })
        {
            Assert.True(tracks.ContainsKey(name), $"missing track '{name}'");
            Assert.NotEmpty(tracks[name]);

            var rms = FullRms(Mixer.RenderTrack(tracks[name], SampleRate));
            Assert.True(rms > 0.05, $"track '{name}' is near-silent: RMS {rms:F5}");
        }
    }

    [Fact]
    public void FullSong_RendersInOnePassWithoutThrowing()
    {
        var bytes = WaveRenderer.RenderToBytes(ParseSsw(FourPartSong));
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void FullSong_IsByteIdenticalAcrossRuns()
    {
        var first = WaveRenderer.RenderToBytes(ParseSsw(FourPartSong));
        var second = WaveRenderer.RenderToBytes(ParseSsw(FourPartSong));

        Assert.Equal(first, second);
    }

    [Fact]
    public void OnDiskExample_RendersAllFourPartsThroughTheWaveRail()
    {
        // Locks the CLI example (`soundscript wave examples/full-song-wave.ss`)
        // so an edit that reintroduces a silent/aborting part is caught here.
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../examples/full-song-wave.ss"));
        var source = File.ReadAllText(path);

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        foreach (var name in new[] { "melody", "harmony", "bass", "choirline" })
        {
            Assert.True(tracks.ContainsKey(name), $"missing track '{name}'");
            var rms = FullRms(Mixer.RenderTrack(tracks[name], SampleRate));
            Assert.True(rms > 0.05, $"track '{name}' is near-silent: RMS {rms:F5}");
        }

        Assert.NotEmpty(WaveRenderer.RenderToBytes(ParseSsw(source)));
    }

    // A compact four-part song touching all three fixes: melody in phrase{}
    // blocks (Bug 1), a strummed chord in the harmony (Bug 3), and a
    // voice { sing } choir line (Bug 2), over a plain bass.
    private const string FourPartSong = """
        tempo 132
        time 4/4

        block hook {
            E4 q E4 q E4 h
            E4 q G4 q C4 q D4 q
        }

        pattern strumPat {
            strum
        }

        track melody {
            mf
            phrase {
                curve soft
                play hook
            }
            phrase {
                f
                E4 q G4 q C4:1.5 D4 e
            }
        }

        track harmony {
            p
            Cmaj w Cmaj w
            play strumPat Cmaj w
            Fmaj w
        }

        track bass {
            mf
            C2 w C2 w
            G2 w F2 w
        }

        voice choirline {
            vocal choir
            mf
            sing "Jingle bells jingle bells" E4 q E4 q E4 h E4 q E4 q E4 h
            sing "Jingle all the way" E4 q G4 q C4 q D4 q E4 w
        }
        """;
}
