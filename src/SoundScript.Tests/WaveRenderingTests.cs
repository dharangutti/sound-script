// UNDER DEVELOPMENT — v1 prototype
// Minimal internal verification harness for SoundScript.Wave (the .ssw ->
// .wav rail with no MIDI step). Not a public CLI — just enough coverage to
// prove the pipeline produces valid, deterministic audio "by ear/waveform".
using System.Text;
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Mixing;
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveRenderingTests
{
    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    [Fact]
    public void RenderToBytes_ProducesValidWavHeader()
    {
        const string source = """
            melody {
                bpm 100
                C4 q
                D4 q
                E4 h
            }
            """;

        var bytes = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(bytes, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20)); // PCM format tag
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22)); // mono
        Assert.Equal(WavWriterSampleRate, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34)); // bits per sample
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));

        var dataSize = BitConverter.ToInt32(bytes, 40);
        Assert.True(dataSize > 0);
        Assert.Equal(44 + dataSize, bytes.Length);
    }

    [Fact]
    public void RenderToBytes_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            track pad {
                tempo 90 → 130 over 2 bars
                Cmaj q
                Dm q
                G7 h
            }
            """;

        var program = ParseSsw(source);
        var first = WaveRenderer.RenderToBytes(program);
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Render_WritesReadableWavFile()
    {
        const string source = """
            melody {
                C4 q
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"soundscript-wave-{Guid.NewGuid():N}.wav");
        try
        {
            WaveRenderer.Render(ParseSsw(source), path);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.True(bytes.Length > 44);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Adapter_DefaultsToSineAndNeutralAdsr_WhenNoTimbreDirectiveExists()
    {
        const string source = """
            melody {
                C4 q
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        var note = Assert.Single(tracks["melody"]);

        Assert.Equal(TimbreParams.Default, note.Timbre);
        Assert.Equal(OscillatorType.Sine, note.Timbre.Oscillator);
    }

    [Fact]
    public void Adapter_ConvertsMiddleAToStandardConcertPitch()
    {
        const string source = """
            melody {
                A4 q
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        var note = Assert.Single(tracks["melody"]);

        Assert.Equal(440.0, note.FrequencyHz, 6);
    }

    [Fact]
    public void Adapter_ChordProducesSimultaneousNoteEventsAtSamePitchless_StartTime()
    {
        const string source = """
            melody {
                Cmaj q
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        var notes = tracks["melody"];

        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Equal(notes[0].StartTimeSeconds, n.StartTimeSeconds, 9));
    }

    [Fact]
    public void Adapter_RestAdvancesTimeWithoutEmittingANote()
    {
        const string source = """
            melody {
                C4 q
                rest q
                D4 q
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        var notes = tracks["melody"];

        Assert.Equal(2, notes.Count);
        Assert.True(notes[1].StartTimeSeconds > notes[0].StartTimeSeconds + notes[0].DurationSeconds);
    }

    [Fact]
    public void Envelope_HandlesNotesShorterThanAttackPlusDecay()
    {
        var slowAttackDecay = new Adsr(Attack: 1.0, Decay: 1.0, Sustain: 0.5, Release: 0.2);
        const double tinyNoteDuration = 0.05;

        // Must not throw (no divide-by-zero) and must stay within [0,1] across the whole note + release tail.
        for (var t = -0.1; t <= tinyNoteDuration + slowAttackDecay.Release + 0.1; t += 0.01)
        {
            var amplitude = Envelope.Amplitude(slowAttackDecay, t, tinyNoteDuration);
            Assert.InRange(amplitude, 0.0, 1.0);
        }

        Assert.Equal(0.0, Envelope.Amplitude(slowAttackDecay, -1.0, tinyNoteDuration));
    }

    [Fact]
    public void Mixer_PeakNormalizesOnlyWhenClippingWouldOccur()
    {
        var loud = new[] { 1.5f, -1.5f, 0.5f };
        var normalized = Mixer.MixTracks([loud]);

        Assert.All(normalized, s => Assert.InRange(s, -1.0f, 1.0f));
        Assert.Equal(1.0f, normalized[0], 3);
    }

    [Fact]
    public void MidiOnlyStyleScript_StillRendersInsteadOfFailing()
    {
        // No .ssw-specific syntax exists yet — any valid .ss script (including
        // ones written for the MIDI rail) must still parse and render here,
        // with a flat default timbre, per the v1 adapter contract.
        const string source = """
            tempo 120
            instrument piano

            melody {
                p
                C4 q D4 q
                mf
                E4 q F4 q
            }
            """;

        var bytes = WaveRenderer.RenderToBytes(ParseSsw(source));
        Assert.True(bytes.Length > 44);
    }

    private const int WavWriterSampleRate = 44_100;
}
