// UNDER DEVELOPMENT — v3
// Verification for WaveSpeechTimeline: the playback-only speech overlay that
// lets `speak "..."` in a wave-rendered program trigger real browser speech
// synthesis in parallel with the baked prosody tones. This walks the same AST
// as AstToNoteEventAdapter, so these tests assert that each speak phrase is
// timed at the beat cursor where its prosody tones are emitted. WAV rendering
// is untouched (determinism is covered by WaveDeterminismTests, run unmodified).
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave.Prosody;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveSpeechTimelineTests
{
    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    // Recompute expected timing independently from the SUT: same tempo math the
    // adapter uses (a fresh TempoAutomationMap set to `bpm`), and the same
    // prosody generator the WAV bakes, summed over its tone durations.
    private static (double StartMs, double DurationMs) ExpectedSpeak(
        string text, string voice, int? seed, int bpm, double startBeat)
    {
        var map = new TempoAutomationMap();
        map.SetTempo(0, bpm);

        var totalBeats = 0.0;
        foreach (var tone in ProsodyToneGenerator.Generate(text, voice, seed))
            totalBeats += tone.DurationBeats;

        return (map.BeatsToMilliseconds(0, startBeat), map.BeatsToMilliseconds(startBeat, totalBeats));
    }

    [Fact]
    public void Build_ReproductionScript_ProducesOneWordSpokenAtDefaultTrackCursor()
    {
        // The repro from the bug report: a 4-note `melody` track followed by a
        // top-level `speak`. The top-level speak uses the separate `default`
        // track (untouched by the melody track), so it starts at beat 0 — NOT
        // after the melody's 4 beats. This mirrors AstToNoteEventAdapter, where
        // top-level statements advance the `default` track independently.
        const string source = """
            tempo 120
            track melody {
                humanize timing=0.02 velocity=0.1 seed=42
                mf
                C4 q D4 q E4 q F4 q
            }
            speak "played by hand" seed=42
            """;

        var words = WaveSpeechTimeline.Build(ParseSsw(source));

        var word = Assert.Single(words);
        Assert.Equal("played by hand", word.Text);
        Assert.Equal(60, word.Midi);

        var (expectedStartMs, expectedDurationMs) =
            ExpectedSpeak("played by hand", "default", 42, bpm: 120, startBeat: 0.0);

        Assert.Equal(0.0, expectedStartMs, 6);          // sanity: default track is at beat 0
        Assert.Equal(expectedStartMs, word.StartMs, 6);
        Assert.Equal(expectedDurationMs, word.DurationMs, 6);
        Assert.True(word.DurationMs > 0);
    }

    [Fact]
    public void Build_SpeakInsideTrackBody_IsTimedAfterThatTracksNotes()
    {
        // A `speak` INSIDE the track body advances that track's cursor, so it
        // must be timed after the preceding four quarter notes (4 beats).
        const string source = """
            tempo 120
            track melody {
                mf
                C4 q D4 q E4 q F4 q
                speak "played by hand" seed=42
            }
            """;

        var words = WaveSpeechTimeline.Build(ParseSsw(source));

        var word = Assert.Single(words);
        Assert.Equal("played by hand", word.Text);

        // Four quarter notes at 120 BPM = 4 beats = 2000 ms.
        var (expectedStartMs, expectedDurationMs) =
            ExpectedSpeak("played by hand", "default", 42, bpm: 120, startBeat: 4.0);

        Assert.Equal(2000.0, expectedStartMs, 6);
        Assert.Equal(expectedStartMs, word.StartMs, 6);
        Assert.Equal(expectedDurationMs, word.DurationMs, 6);
    }

    [Fact]
    public void Build_TwoTopLevelSpeaks_AppearInOrderWithDistinctStarts()
    {
        // Two top-level speaks sharing the `default` cursor: the second is
        // timed after the first's prosody-tone span, so both are non-negative
        // and strictly increasing.
        const string source = """
            tempo 120
            speak "hello there" seed=1
            speak "played by hand" seed=2
            """;

        var words = WaveSpeechTimeline.Build(ParseSsw(source));

        Assert.Equal(2, words.Count);
        Assert.Equal("hello there", words[0].Text);
        Assert.Equal("played by hand", words[1].Text);

        Assert.Equal(0.0, words[0].StartMs, 6);
        Assert.True(words[0].StartMs >= 0);
        Assert.True(words[1].StartMs > words[0].StartMs);

        // Second speak's onset equals the first speak's onset + its duration.
        var (firstStart, firstDuration) = ExpectedSpeak("hello there", "default", 1, bpm: 120, startBeat: 0.0);
        Assert.Equal(firstStart + firstDuration, words[1].StartMs, 6);
    }

    [Fact]
    public void Build_SpeakBeforeAndAfterATrack_YieldsTwoOrderedWords()
    {
        // A top-level speak before a track and another after it. Both use the
        // `default` cursor (the intervening `track` block has its own cursor),
        // so they still compose sequentially on the default track.
        const string source = """
            tempo 120
            speak "hello there" seed=1
            track melody {
                mf
                C4 q D4 q
            }
            speak "played by hand" seed=2
            """;

        var words = WaveSpeechTimeline.Build(ParseSsw(source));

        Assert.Equal(2, words.Count);
        Assert.Equal("hello there", words[0].Text);
        Assert.Equal("played by hand", words[1].Text);
        Assert.True(words[0].StartMs >= 0);
        Assert.True(words[1].StartMs > words[0].StartMs);
    }

    [Fact]
    public void Build_NoSpeak_ReturnsEmpty()
    {
        const string source = """
            tempo 120
            track melody {
                mf
                C4 q D4 q E4 q F4 q
            }
            effect delay time=0.25 feedback=0.35 mix=0.3
            """;

        Assert.Empty(WaveSpeechTimeline.Build(ParseSsw(source)));
    }

    [Fact]
    public void Build_IsDeterministic_AcrossRepeatedCalls()
    {
        const string source = """
            tempo 120
            speak "played by hand" seed=42
            """;

        var first = WaveSpeechTimeline.Build(ParseSsw(source));
        var second = WaveSpeechTimeline.Build(ParseSsw(source));

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Text, second[i].Text);
            Assert.Equal(first[i].StartMs, second[i].StartMs, 9);
            Assert.Equal(first[i].DurationMs, second[i].DurationMs, 9);
            Assert.Equal(first[i].Midi, second[i].Midi);
        }
    }
}
