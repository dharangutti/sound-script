// UNDER DEVELOPMENT — v3
// Verification for SoundScript.Wave v3: master effects chain (delay +
// single-pole filter grammar, post-mix processing, spectral behavior),
// seeded humanize jitter (named-parameter extension of the shared humanize
// directive), and phoneme/prosody tone mapping ('speak') — each with its own
// determinism regression, plus one combined test (all three together) per
// the v3 spec. WaveRenderingTests (v1) and WaveV2Tests are intentionally
// untouched; their passing unmodified proves the pre-v3 paths survived.
using SoundScript.Core.Ast;
using SoundScript.Parser;
using SoundScript.Wave;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Effects;
using SoundScript.Wave.Model;
using SoundScript.Wave.Mixing;
using SoundScript.Wave.Prosody;
using SoundScript.Wave.Synthesis;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class WaveV3Tests
{
    private const int SampleRate = 44_100;

    private static ProgramNode ParseSsw(string source) =>
        new SoundScriptParser(new Tokenizer(source).Tokenize()).Parse();

    // ---- Grammar: effect ----

    [Fact]
    public void Parse_EffectDelayAndFilter_ProducesEffectNodes()
    {
        const string source = """
            effect delay time=0.25 feedback=0.4 mix=0.3
            effect filter type=lowpass cutoff=2000
            """;

        var program = ParseSsw(source);

        var delay = Assert.IsType<EffectNode>(program.Statements[0]);
        Assert.Equal("delay", delay.Kind);
        Assert.Equal("0.25", delay.Parameters["time"]);
        Assert.Equal("0.4", delay.Parameters["feedback"]);
        Assert.Equal("0.3", delay.Parameters["mix"]);

        var filter = Assert.IsType<EffectNode>(program.Statements[1]);
        Assert.Equal("filter", filter.Kind);
        Assert.Equal("lowpass", filter.Parameters["type"]);
        Assert.Equal("2000", filter.Parameters["cutoff"]);
    }

    [Fact]
    public void Parse_EffectReverb_FailsAsExplicitlyDeferred()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ParseSsw("effect reverb mix=0.3"));

        Assert.Contains("deferred", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delay", ex.Message);
        Assert.Contains("filter", ex.Message);
    }

    [Fact]
    public void Parse_EffectRejectsUnknownKindAndBadParameters()
    {
        Assert.Throws<InvalidOperationException>(() => ParseSsw("effect chorus depth=0.5"));
        Assert.Throws<InvalidOperationException>(() => ParseSsw("effect delay feedback=0.4"));       // missing time=
        Assert.Throws<InvalidOperationException>(() => ParseSsw("effect delay time=0.25 wet=0.3"));  // unknown key
        Assert.Throws<InvalidOperationException>(() => ParseSsw("effect delay time=0.25 feedback=1.0")); // >= 1.0
        Assert.Throws<InvalidOperationException>(() => ParseSsw("effect filter type=bandpass cutoff=900"));
    }

    // ---- Grammar: humanize (both forms) ----

    [Fact]
    public void Parse_HumanizeBareNumber_IsUnchanged()
    {
        const string source = """
            track piano {
                humanize 0.03
                C4 q
            }
            """;

        var track = Assert.IsType<TrackNode>(ParseSsw(source).Statements[0]);
        var humanize = Assert.IsType<HumanizeNode>(track.Body[0]);

        Assert.Equal(0.03, humanize.Value);
        Assert.Null(humanize.Timing);
        Assert.Null(humanize.VelocityAmount);
        Assert.Null(humanize.Seed);
    }

    [Fact]
    public void Parse_HumanizeNamedForm_CarriesAllParameters()
    {
        const string source = """
            track piano {
                humanize timing=0.02 velocity=0.1 seed=42
                C4 q
            }
            """;

        var track = Assert.IsType<TrackNode>(ParseSsw(source).Statements[0]);
        var humanize = Assert.IsType<HumanizeNode>(track.Body[0]);

        Assert.Equal(0.02, humanize.Timing);
        Assert.Equal(0.1, humanize.VelocityAmount);
        Assert.Equal(42, humanize.Seed);
        // MIDI back-compat mapping: Value carries the timing magnitude.
        Assert.Equal(0.02, humanize.Value);
    }

    [Fact]
    public void Parse_HumanizeNamedForm_RejectsInvalidInput()
    {
        // seed alone has nothing to vary
        Assert.Throws<InvalidOperationException>(() => ParseSsw("track t { humanize seed=1 \n C4 q }"));
        // unknown key
        Assert.Throws<InvalidOperationException>(() => ParseSsw("track t { humanize swing=0.1 \n C4 q }"));
        // out-of-range velocity fraction
        Assert.Throws<InvalidOperationException>(() => ParseSsw("track t { humanize velocity=1.5 \n C4 q }"));
        // nothing at all
        Assert.Throws<InvalidOperationException>(() => ParseSsw("track t { humanize \n C4 q }"));
    }

    // ---- Grammar: speak ----

    [Fact]
    public void Parse_Speak_CarriesTextVoiceAndSeed()
    {
        var program = ParseSsw("speak \"hello world\" voice=default seed=7");
        var speak = Assert.IsType<SpeakNode>(program.Statements[0]);

        Assert.Equal("hello world", speak.Text);
        Assert.Equal("default", speak.Voice);
        Assert.Equal(7, speak.Seed);

        // voice/seed optional
        var minimal = Assert.IsType<SpeakNode>(ParseSsw("speak \"hi\"").Statements[0]);
        Assert.Equal("default", minimal.Voice);
        Assert.Null(minimal.Seed);
    }

    [Fact]
    public void Parse_SpeakRejectsLetterlessText()
    {
        Assert.Throws<InvalidOperationException>(() => ParseSsw("speak \"123 !?\""));
    }

    [Fact]
    public void Parse_EffectAndSpeak_StillWorkAsPlainNames()
    {
        // Back-compat: the new keywords must stay usable wherever an
        // identifier-like name was previously valid (ParseName).
        const string source = """
            track effect {
                C4 q
            }
            sequence speak {
                D4 q
            }
            """;

        var program = ParseSsw(source);
        Assert.Equal("effect", Assert.IsType<TrackNode>(program.Statements[0]).Name);
        Assert.Equal("speak", Assert.IsType<SequenceNode>(program.Statements[1]).Name);
    }

    // ---- MIDI backend: clear rejection, not silence or crash ----

    [Fact]
    public void MidiInterpreter_RejectsEffect_WithWaveBackendError()
    {
        var program = ParseSsw("effect delay time=0.25");

        var ex = Assert.Throws<NotSupportedException>(() => Midi.Interpreter.Interpret(program));
        Assert.Contains("wave", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiInterpreter_RejectsSpeak_WithWaveBackendError()
    {
        var program = ParseSsw("speak \"hello\" seed=1");

        var ex = Assert.Throws<NotSupportedException>(() => Midi.Interpreter.Interpret(program));
        Assert.Contains("wave", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MidiInterpreter_AcceptsHumanizeNamedForm()
    {
        // The named form must not crash the MIDI path: Value carries the
        // timing magnitude, seed is deliberately ignored there.
        const string source = """
            tempo 120
            track piano {
                humanize timing=0.02 velocity=0.1 seed=42
                C4 q
            }
            """;

        var interpreted = Midi.Interpreter.Interpret(ParseSsw(source));
        Assert.Single(interpreted.Tracks);
        Assert.Single(interpreted.Tracks[0].Notes);
    }

    // ---- Effects chain: determinism + DSP behavior ----

    [Fact]
    public void EffectsChain_EndToEnd_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            melody {
                bpm 100
                C4 q
                E4 q
                G4 h
            }
            effect delay time=0.2 feedback=0.35 mix=0.4
            effect filter type=lowpass cutoff=2500
            """;

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Delay_ExtendsOutputWithAnEchoTail()
    {
        var note = new NoteEvent(440.0, 0.0, 0.2, 1.0, TimbreParams.Default);
        var dryBuffer = Mixer.RenderTrack([note], SampleRate);

        var effects = new List<EffectSettings> { new DelaySettings(0.25, 0.4, 0.5) };
        var wet = MasterEffectChain.Apply(dryBuffer, effects, SampleRate);

        Assert.True(wet.Length > dryBuffer.Length, "Delay must append an echo tail, not truncate it");

        // The region past the dry length must actually contain echoes.
        var tailEnergy = 0.0;
        for (var i = dryBuffer.Length; i < wet.Length; i++)
            tailEnergy += (double)wet[i] * wet[i];

        Assert.True(tailEnergy > 0.0, "Echo tail is silent — the delay line isn't feeding back");
    }

    [Fact]
    public void Delay_WithZeroMix_IsTransparent()
    {
        var note = new NoteEvent(440.0, 0.0, 0.2, 0.8, TimbreParams.Default);
        var dryBuffer = Mixer.RenderTrack([note], SampleRate);

        var effects = new List<EffectSettings> { new DelaySettings(0.25, 0.5, 0.0) };
        var wet = MasterEffectChain.Apply(dryBuffer, effects, SampleRate);

        Assert.Equal(dryBuffer.Length, wet.Length);
        Assert.Equal(dryBuffer, wet);
    }

    [Fact]
    public void LowpassFilter_AttenuatesToneWellAboveCutoff()
    {
        // 5 kHz sine through a 500 Hz single-pole lowpass: |H| ≈ 0.10 at
        // 10× the cutoff (6 dB/octave), so RMS must collapse.
        var tone = RenderFlatSine(5000.0);
        var filtered = MasterEffectChain.Apply(
            tone, [new FilterSettings(FilterKind.LowPass, 500.0)], SampleRate);

        Assert.True(Rms(filtered) < 0.2 * Rms(tone),
            $"Lowpass barely attenuated: {Rms(filtered)} vs {Rms(tone)}");
    }

    [Fact]
    public void HighpassFilter_AttenuatesToneWellBelowCutoff()
    {
        // 200 Hz sine through a 5 kHz single-pole highpass.
        var tone = RenderFlatSine(200.0);
        var filtered = MasterEffectChain.Apply(
            tone, [new FilterSettings(FilterKind.HighPass, 5000.0)], SampleRate);

        Assert.True(Rms(filtered) < 0.2 * Rms(tone),
            $"Highpass barely attenuated: {Rms(filtered)} vs {Rms(tone)}");

        // ...and passes a tone well above the cutoff mostly intact.
        var high = RenderFlatSine(15_000.0);
        var passed = MasterEffectChain.Apply(
            high, [new FilterSettings(FilterKind.HighPass, 5000.0)], SampleRate);

        Assert.True(Rms(passed) > 0.5 * Rms(high),
            $"Highpass wrongly attenuated its passband: {Rms(passed)} vs {Rms(high)}");
    }

    [Fact]
    public void EffectsChain_StereoPath_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            track pad {
                Cmaj q
                G7 h
            }
            effect delay time=0.15 feedback=0.3 mix=0.3
            effect filter type=highpass cutoff=300
            """;

        var first = WaveRenderer.RenderStereoToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderStereoToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    // ---- Seeded jitter: determinism ----

    [Fact]
    public void Humanize_SameSeed_IsByteIdenticalAcrossRuns()
    {
        var first = WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 42)));
        var second = WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 42)));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Humanize_DifferentSeeds_ProduceDifferentButDeterministicTakes()
    {
        var seed1 = WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 1)));
        var seed2 = WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 2)));

        Assert.NotEqual(seed1, seed2);

        // Each take is individually reproducible.
        Assert.Equal(seed2, WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 2))));
    }

    [Fact]
    public void Humanize_OmittedSeed_IsDerivedDeterministically()
    {
        // No seed= anywhere: the wave backend derives one from the track
        // name — never wall-clock — so repeat renders stay byte-identical.
        const string source = """
            track piano {
                humanize timing=0.02 velocity=0.1
                C4 q
                E4 q
                G4 q
            }
            """;

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Humanize_ActuallyPerturbsTheRender()
    {
        const string plain = """
            track piano {
                C4 q
                E4 q
                G4 q
            }
            """;

        var unjittered = WaveRenderer.RenderToBytes(ParseSsw(plain));
        var jittered = WaveRenderer.RenderToBytes(ParseSsw(HumanizedSource(seed: 42)));

        Assert.NotEqual(unjittered, jittered);
    }

    [Fact]
    public void Humanize_BareNumberForm_JittersDeterministicallyOnWaveRail()
    {
        // v1 bare form on the wave rail: Value acts as both timing seconds
        // and velocity fraction (mirroring MIDI), seed derived from track name.
        const string source = """
            track piano {
                humanize 0.02
                C4 q
                E4 q
            }
            """;

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    // ---- Prosody: determinism + shape ----

    [Fact]
    public void Speak_SameTextSameSeed_IsByteIdenticalAcrossRuns()
    {
        const string source = "speak \"hello world\" voice=default seed=7";

        var first = WaveRenderer.RenderToBytes(ParseSsw(source));
        var second = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Speak_DifferentSeeds_ProduceDifferentTones()
    {
        var seed7 = WaveRenderer.RenderToBytes(ParseSsw("speak \"hello world\" seed=7"));
        var seed8 = WaveRenderer.RenderToBytes(ParseSsw("speak \"hello world\" seed=8"));

        Assert.NotEqual(seed7, seed8);
    }

    [Fact]
    public void Speak_OmittedSeed_IsDerivedFromTextDeterministically()
    {
        var first = WaveRenderer.RenderToBytes(ParseSsw("speak \"good morning\""));
        var second = WaveRenderer.RenderToBytes(ParseSsw("speak \"good morning\""));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Speak_EmitsPhonemeTonesWithinTheTableBands()
    {
        var tracks = AstToNoteEventAdapter.Convert(ParseSsw("speak \"hello world\" seed=7"));
        var notes = tracks["default"];

        // "hello world" → h ee l au | w au r l d = 9 phoneme tones
        // (doubled 'l' collapses; the inter-word gap is a rest, not a note).
        Assert.Equal(9, notes.Count);

        foreach (var note in notes)
        {
            // Whole table (including the fallback row) lives in ~160-330 Hz.
            Assert.InRange(note.FrequencyHz, 150.0, 340.0);
            Assert.True(note.DurationSeconds > 0);
            Assert.InRange(note.Velocity, 0.0, 1.0);
        }

        // Free-form Hz, not MIDI-quantized: same word, two different vowels
        // drawn from the same band must not collapse to identical pitches.
        Assert.NotEqual(notes[1].FrequencyHz, notes[3].FrequencyHz);
    }

    [Fact]
    public void Speak_SameSeed_YieldsIdenticalToneSequences()
    {
        var first = ProsodyToneGenerator.Generate("hello world", "default", 7);
        var second = ProsodyToneGenerator.Generate("hello world", "default", 7);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Speak_UnknownVoice_FailsWithClearError()
    {
        var program = ParseSsw("speak \"hello\" voice=alto seed=1");

        var ex = Assert.Throws<NotSupportedException>(() => WaveRenderer.RenderToBytes(program));
        Assert.Contains("default", ex.Message);
    }

    // ---- All three features together (interaction regression) ----

    [Fact]
    public void CombinedEffectsJitterAndProsody_IsByteIdenticalAcrossRuns()
    {
        const string source = """
            tempo 110
            track lead {
                humanize timing=0.01 velocity=0.05 seed=42
                C4 q
                E4 q
                G4 h
            }
            speak "hello world" voice=default seed=7
            effect delay time=0.2 feedback=0.3 mix=0.4
            effect filter type=lowpass cutoff=3000
            """;

        var firstMono = WaveRenderer.RenderToBytes(ParseSsw(source));
        var secondMono = WaveRenderer.RenderToBytes(ParseSsw(source));
        Assert.Equal(firstMono, secondMono);

        var firstStereo = WaveRenderer.RenderStereoToBytes(ParseSsw(source));
        var secondStereo = WaveRenderer.RenderStereoToBytes(ParseSsw(source));
        Assert.Equal(firstStereo, secondStereo);
    }

    // ---- Shared PRNG sanity ----

    [Fact]
    public void DeterministicRandom_IsStableInRangeAndSeedSensitive()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.InRange(DeterministicRandom.Unit01(42, i, 0), 0.0, 1.0);
            Assert.InRange(DeterministicRandom.Unit(42, i, 1), -1.0, 1.0);
        }

        Assert.Equal(DeterministicRandom.Unit01(42, 3, 1), DeterministicRandom.Unit01(42, 3, 1));
        Assert.NotEqual(DeterministicRandom.Unit01(42, 3, 1), DeterministicRandom.Unit01(43, 3, 1));
        Assert.NotEqual(DeterministicRandom.Unit01(42, 3, 1), DeterministicRandom.Unit01(42, 4, 1));
        Assert.NotEqual(DeterministicRandom.Unit01(42, 3, 1), DeterministicRandom.Unit01(42, 3, 2));

        Assert.Equal(DeterministicRandom.DeriveSeed("piano"), DeterministicRandom.DeriveSeed("piano"));
        Assert.NotEqual(DeterministicRandom.DeriveSeed("piano"), DeterministicRandom.DeriveSeed("drums"));
    }

    // ---- Helpers ----

    private static string HumanizedSource(int seed) => $$"""
        track piano {
            humanize timing=0.02 velocity=0.1 seed={{seed}}
            C4 q
            E4 q
            G4 q
        }
        """;

    private static float[] RenderFlatSine(double frequencyHz)
    {
        var note = new NoteEvent(
            FrequencyHz: frequencyHz,
            StartTimeSeconds: 0.0,
            DurationSeconds: 0.3,
            Velocity: 1.0,
            Timbre: TimbreParams.Default with
            {
                // Flat envelope so the RMS comparison sees a pure steady tone.
                Envelope = new Adsr(Attack: 0.0, Decay: 0.0, Sustain: 1.0, Release: 0.0)
            });

        return Mixer.RenderTrack([note], SampleRate);
    }

    // Skip the filter's initial transient before measuring.
    private static double Rms(float[] samples)
    {
        const int skip = 2000;
        Assert.True(samples.Length > skip + 1000, "Buffer too short for RMS window");

        var sum = 0.0;
        for (var i = skip; i < samples.Length; i++)
            sum += (double)samples[i] * samples[i];

        return Math.Sqrt(sum / (samples.Length - skip));
    }
}
