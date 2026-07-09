// UNDER DEVELOPMENT — v3
// Verification for SoundScript.Wave v3: master effects chain (delay +
// single-pole filter grammar, post-mix processing, spectral behavior),
// seeded humanize jitter (named-parameter extension of the shared humanize
// directive), and phoneme/prosody tone mapping ('speak') — each with its own
// determinism regression, plus one combined test (all three together) per
// the v3 spec. WaveRenderingTests (v1) and WaveV2Tests are intentionally
// untouched; their passing unmodified proves the pre-v3 paths survived.
using System.Linq;
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

// shares the HumanizeSeed collection: SetSeed mutates process-wide state
[Collection("HumanizeSeed")]
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
    public void Parse_SpeakInsideTrackBody_IsAllowed()
    {
        // Regression: 'speak' is per-track (unlike the genuinely master-level
        // 'effect'), so it must parse inside track/sequence/block/loop bodies,
        // not just at the top level.
        const string source = """
            track vocals {
                speak "hello" seed=1
            }
            """;

        var track = Assert.IsType<TrackNode>(ParseSsw(source).Statements[0]);
        Assert.IsType<SpeakNode>(track.Body[0]);
    }

    [Fact]
    public void WaveAdapter_SpeakInsideTrackBody_EmitsIntoThatTrack()
    {
        const string source = """
            track vocals {
                speak "hi" seed=1
            }
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        Assert.True(tracks.ContainsKey("vocals"));
        Assert.NotEmpty(tracks["vocals"]);
    }

    [Fact]
    public void MidiInterpreter_RejectsSpeakInsideTrackBody_WithWaveBackendError()
    {
        var program = ParseSsw("""
            track vocals {
                speak "hello" seed=1
            }
            """);

        var ex = Assert.Throws<NotSupportedException>(() => Midi.Interpreter.Interpret(program));
        Assert.Contains("wave", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WaveAdapter_RejectsMelodyShorthandCollidingWithExplicitTrackNamedMelody()
    {
        // Regression: same collision as the MIDI-side test in
        // TrackMetadataTests, verified on the wave rail's AstToNoteEventAdapter.
        const string source = """
            melody {
                C4 q
            }
            track melody {
                D4 q
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => AstToNoteEventAdapter.Convert(ParseSsw(source)));
        Assert.Contains("Duplicate track name", ex.Message);
    }

    [Fact]
    public void MidiInterpreter_AcceptsHumanizeNamedForm()
    {
        // The named form must not crash the MIDI path: timing/velocity are
        // resolved independently via HumanizeNode.Resolve, seed is
        // deliberately ignored there.
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

    [Fact]
    public void MidiInterpreter_HumanizeVelocityOnly_StillJittersVelocity()
    {
        // Regression: velocity=-only named form used to resolve to Value=0.0
        // (Timing was null), silently zeroing out the requested velocity
        // jitter on the MIDI backend.
        const string source = """
            track piano {
                humanize velocity=0.5
                mf
                C4 q
            }
            """;

        Midi.HumanizeApplicator.SetSeed(42);
        var humanized = Midi.Interpreter.Interpret(ParseSsw(source)).Tracks.Single().Notes.Single();
        var dry = Midi.Interpreter.Interpret(ParseSsw("""
            track piano {
                mf
                C4 q
            }
            """)).Tracks.Single().Notes.Single();

        Assert.NotEqual(dry.Velocity, humanized.Velocity);
    }

    [Fact]
    public void MidiInterpreter_HumanizeTimingOnly_DoesNotJitterVelocity()
    {
        // Regression: timing=-only named form used to leak the timing
        // magnitude into the velocity channel via the shared Value fallback,
        // jittering velocity even though only timing was requested.
        const string source = """
            track piano {
                humanize timing=0.5
                mf
                C4 q
            }
            """;

        Midi.HumanizeApplicator.SetSeed(42);
        var humanized = Midi.Interpreter.Interpret(ParseSsw(source)).Tracks.Single().Notes.Single();
        var dry = Midi.Interpreter.Interpret(ParseSsw("""
            track piano {
                mf
                C4 q
            }
            """)).Tracks.Single().Notes.Single();

        Assert.Equal(dry.Velocity, humanized.Velocity);
    }

    [Fact]
    public void WaveAdapter_HumanizeTimingOnly_DoesNotJitterVelocity()
    {
        // Same regression as the MIDI-side test above, verified on the wave
        // rail's AstToNoteEventAdapter: timing=-only must not leak into velocity.
        const string source = """
            track piano {
                humanize timing=0.5 seed=1
                C4 q
                E4 q
                G4 q
            }
            """;
        const string dry = """
            track piano {
                C4 q
                E4 q
                G4 q
            }
            """;

        var notes = AstToNoteEventAdapter.Convert(ParseSsw(source))["piano"];
        var dryNotes = AstToNoteEventAdapter.Convert(ParseSsw(dry))["piano"];

        for (var i = 0; i < notes.Count; i++)
            Assert.Equal(dryNotes[i].Velocity, notes[i].Velocity);
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

    [Theory]
    [MemberData(nameof(SupportedEffectKinds))]
    public void Effect_EachSupportedKind_RendersThroughTheFullPipeline(string kind)
    {
        // Safety net for the effect-kind duplication across Parser,
        // EffectSettingsFactory, and MasterEffectChain (three independent
        // switches with no shared source of truth beyond EffectKinds.All):
        // this iterates every kind the grammar claims to support through the
        // full parse -> adapt -> DSP pipeline, so adding a kind to
        // EffectKinds.All without wiring it all the way through fails here
        // immediately instead of only failing at render time for whoever
        // happens to use the new kind first.
        var effectStatement = kind switch
        {
            "delay" => "effect delay time=0.1 feedback=0.3 mix=0.4",
            "filter" => "effect filter type=lowpass cutoff=2000",
            _ => throw new NotSupportedException(
                $"Test doesn't know how to build a minimal '{kind}' effect statement — " +
                "add a case here when EffectKinds.All grows.")
        };
        var source = $$"""
            melody {
                C4 q
            }
            {{effectStatement}}
            """;

        var bytes = WaveRenderer.RenderToBytes(ParseSsw(source));

        Assert.NotEmpty(bytes);
    }

    public static IEnumerable<object[]> SupportedEffectKinds() =>
        EffectKinds.All.Select(kind => new object[] { kind });

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
    public void Delay_WithHighFeedback_TailDecaysBelowFloorInsteadOfTruncatingAbruptly()
    {
        // Regression: a 64-repeat hard cap used to stop the tail well before
        // it decayed below the audible floor for any feedback above ~0.82
        // (e.g. 0.95 left the last echo at ~4% of full scale), producing an
        // abrupt discontinuity/click instead of a smooth fade-out.
        var impulse = new double[] { 1.0 };
        var settings = new DelaySettings(TimeSeconds: 0.01, Feedback: 0.95, Mix: 1.0);

        var output = DelayEffect.Process(impulse, settings, sampleRate: 1000);
        var lastEchoAmplitude = Math.Abs(output[^1]);

        // The old 64-repeat cap left the last echo around 0.0395 for this
        // feedback; the fix should land within roughly an order of
        // magnitude of the 1e-4 floor rather than 400x above it.
        Assert.True(lastEchoAmplitude < 0.001,
            $"Expected the tail to decay close to the audible floor, but the last echo was {lastEchoAmplitude}");
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
    public void Speak_EmitsClassAppropriateTimbrePerPhoneme()
    {
        var tracks = AstToNoteEventAdapter.Convert(ParseSsw("speak \"hello world\" seed=7"));
        var notes = tracks["default"];

        // "hello world" → h(fricative) ee(vowel) l(liquid) au(vowel) |
        // w(nasal) au(vowel) r(liquid) l(liquid) d(plosive) = 9 phonemes
        // (doubled 'l' collapses; the inter-word gap is a rest, not a note).
        // Each of the 3 vowels (ee, au, au) also emits a stacked formant-ish
        // overtone note, so the track carries 9 + 3 = 12 NoteEvents.
        Assert.Equal(12, notes.Count);

        var noiseNotes = notes.Where(n => n.Timbre.Oscillator == OscillatorType.Noise).ToList();
        var tonalNotes = notes.Where(n => n.Timbre.Oscillator != OscillatorType.Noise).ToList();

        // h and d are the only noise-class (fricative/plosive) phonemes here.
        Assert.Equal(2, noiseNotes.Count);
        Assert.Equal(10, tonalNotes.Count);

        // Noise notes carry a filter-cutoff "frequency" in the consonant band,
        // not a conversational pitch.
        foreach (var note in noiseNotes)
            Assert.InRange(note.FrequencyHz, 1200.0, 6000.0);

        // Tonal notes (vowel fundamental + formant overtone, nasal, liquid)
        // sit in the conversational band; a vowel's formant partial runs up
        // to 2.5x the fundamental's ~330 Hz ceiling.
        foreach (var note in tonalNotes)
            Assert.InRange(note.FrequencyHz, 150.0, 850.0);

        foreach (var note in notes)
        {
            Assert.True(note.DurationSeconds > 0);
            Assert.InRange(note.Velocity, 0.0, 1.0);
        }

        // Free-form Hz, not MIDI-quantized: distinct tonal phonemes drawn
        // from the same band must not collapse to identical pitches.
        Assert.True(tonalNotes.Select(n => n.FrequencyHz).Distinct().Count() > 1);
    }

    [Fact]
    public void Speak_StaysAudibleAgainstABusyBackingTrack()
    {
        // Regression for "voice technically present but perceptually buried":
        // speak content is short, transient phoneme blips scheduled on its own
        // beat cursor, competing against continuous, sustained melody/harmony/
        // bass in the same register. The mixdown has no per-track gain and only
        // a single global down-only peak normalization shared by every track,
        // so the voice:instrumental loudness ratio is set purely by the vocal
        // track's own note velocity — which was low enough that a busy
        // arrangement drowned the voice out (ratio ~0.63, roughly -4 dB) even
        // though no sample was literally zero. Assert the speech track carries
        // real energy relative to the full instrumental bed.
        const string source = """
            tempo 132
            time 4/4

            track melody {
                mf
                E4 q E4 q E4 h
                E4 q G4 q C4:1.5 D4 e
            }
            track harmony {
                p
                Cmaj w Cmaj w
                Fmaj w G7 w
            }
            track bass {
                mf
                C2 w C2 w
                F2 w G2 w
            }

            speak "jingle bells jingle bells jingle all the way" seed=13
            """;

        var tracks = AstToNoteEventAdapter.Convert(ParseSsw(source));
        Assert.True(tracks.ContainsKey("default"), "speech emitted no default track");

        // The mix applies one global normalization scalar to every track alike,
        // so it cancels out of the ratio: comparing the solo-rendered voice
        // against the summed instrumental bed measures exactly the audible
        // balance the listener hears in the export.
        var voice = Mixer.RenderTrack(tracks["default"], SampleRate);
        var instrumental = SumBuffers(
            tracks.Where(t => t.Key != "default")
                  .Select(t => Mixer.RenderTrack(t.Value, SampleRate)));

        var ratio = FullRms(voice) / FullRms(instrumental);

        // Achieved ratio is ~0.85 (-1.4 dB) with the presence-tuned velocity;
        // the pre-fix 0.7 velocity yielded ~0.63 (-4 dB). A 0.75 floor sits
        // between the two, so a regression back to a buried voice fails here.
        Assert.True(ratio > 0.75,
            $"Voice is too quiet against the backing track: voice/instrumental RMS ratio {ratio:F3} (expected > 0.75)");
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

    private static float[] SumBuffers(IEnumerable<float[]> buffers)
    {
        var list = buffers.ToList();
        var length = list.Count == 0 ? 0 : list.Max(b => b.Length);
        var summed = new float[length];
        foreach (var buffer in list)
            for (var i = 0; i < buffer.Length; i++)
                summed[i] += buffer[i];

        return summed;
    }

    // Whole-buffer RMS (no transient skip) — for the voice-presence balance check.
    private static double FullRms(float[] samples)
    {
        if (samples.Length == 0)
            return 0.0;

        var sum = 0.0;
        foreach (var s in samples)
            sum += (double)s * s;

        return Math.Sqrt(sum / samples.Length);
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
