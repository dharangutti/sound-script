// UNDER DEVELOPMENT — v3
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Wave.Model;
using SoundScript.Wave.Prosody;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Wave.Adapter;

/// <summary>
/// Walks the existing, unmodified AST (the same one SoundScript.Midi.Interpreter
/// consumes) and produces a <c>List&lt;NoteEvent&gt;</c> per track. This is the
/// only type in SoundScript.Wave allowed to depend on core AST types —
/// everything downstream (Synthesis, Mixing, Io) operates purely on
/// NoteEvent/TimbreParams, decoupled from grammar internals.
///
/// v1 scope: notes, chords, rests, tracks/melody, named sequences/blocks
/// (via "play"), loops, tempo (including ramps), and time signature, with
/// velocity/dynamic markings mapped to 0.0-1.0. Directives that only make
/// sense for MIDI-oriented playback shaping — instrument program changes,
/// gain, orchestration, phrase shaping, chord voicing/balancing
/// intelligence, arpeggio/strum patterns, and vocal (voice/sing) tracks —
/// are not yet implemented. They are silently skipped rather than failing,
/// so a MIDI-only .ss file can still be pushed through this rail and
/// produce a flat, default-timbre rendering instead of an error. Every
/// track gets <see cref="TimbreParams.Default"/> (sine + neutral ADSR)
/// since the grammar has no per-track timbre directive yet.
///
/// v3 additions:
/// <list type="bullet">
/// <item><c>humanize</c> (both forms) now jitters NoteEvent start time and
/// velocity at emit time via the shared seeded PRNG — deterministic, seeded
/// from the directive's seed= or (when omitted) a hash of the track name.
/// This is independent of the MIDI backend's own HumanizeApplicator.</item>
/// <item><c>speak "..."</c> emits a deterministic phoneme-tone NoteEvent
/// sequence into the default track (see Prosody.ProsodyToneGenerator).</item>
/// <item><c>effect ...</c> nodes are deliberately NOT handled here: the
/// effects chain is a master-only post-mix stage consumed by WaveRenderer
/// through EffectSettingsFactory — not a per-track/per-note concern.</item>
/// </list>
/// </summary>
public static class AstToNoteEventAdapter
{
    public static Dictionary<string, List<NoteEvent>> Convert(ProgramNode program)
    {
        var context = new ExecutionContext();
        var tracks = new Dictionary<string, TrackState>(StringComparer.OrdinalIgnoreCase);
        var trackOrder = new List<string>();
        TrackState? defaultTrack = null;

        TrackState GetDefaultTrack()
        {
            defaultTrack ??= GetOrCreateTrack(tracks, trackOrder, "default");
            return defaultTrack;
        }

        var globalTempoBeat = 0.0;
        var declaredTrackNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case BpmNode bpm:
                    context.TempoMap.SetTempo(globalTempoBeat, bpm.Bpm);
                    break;
                case TempoNode tempo:
                    context.TempoMap.SetTempo(globalTempoBeat, tempo.Bpm);
                    break;
                case TempoRampNode ramp:
                    ScheduleTempoRamp(context, globalTempoBeat, ramp);
                    globalTempoBeat += ramp.Bars * GetBeatsPerBar(context);
                    break;
                case TimeSignatureNode time:
                    context.TimeSignatureNumerator = time.Numerator;
                    context.TimeSignatureDenominator = time.Denominator;
                    break;
                case SequenceNode sequence:
                    context.Sequences[sequence.Name] = sequence.Body;
                    break;
                case BlockNode block:
                    context.Blocks[block.Name] = block.Body;
                    break;
                case PatternNode pattern:
                    // Registered only so an unresolved `play` can distinguish
                    // "known but unsupported" from "genuine typo" — see summary.
                    context.PatternNames.Add(pattern.Name);
                    break;
                case TrackNode track:
                    RequireUniqueTrackName(declaredTrackNames, track.Name);
                    ExecuteStatements(GetOrCreateTrack(tracks, trackOrder, track.Name), track.Body, context);
                    break;
                case MelodyNode melody:
                    RequireUniqueTrackName(declaredTrackNames, "melody");
                    ExecuteStatements(GetOrCreateTrack(tracks, trackOrder, "melody"), melody.Body, context);
                    break;
                case PlayNode play:
                    ExecutePlay(GetDefaultTrack(), play, context);
                    break;
                case LoopNode loop:
                    ExecuteLoop(GetDefaultTrack(), loop, context);
                    break;
                case VelocityNode velocity:
                    GetDefaultTrack().CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    GetDefaultTrack().CurrentDynamic = dynamic.Level;
                    break;
                case RestNode rest:
                    AdvanceBeat(GetDefaultTrack(), rest.Rest.DurationBeats);
                    break;
                case NoteNode note:
                    EmitNote(GetDefaultTrack(), note, context);
                    break;
                case ChordNode chord:
                    EmitChord(GetDefaultTrack(), chord, context);
                    break;
                case SpeakNode speak:
                    EmitSpeech(GetDefaultTrack(), speak, context);
                    break;

                // EffectNode: master-only post-mix stage — consumed by
                // WaveRenderer via EffectSettingsFactory, intentionally no
                // per-track handling here (see class summary).
                //
                // InstrumentNode, GainNode, OrchestrationNode, Phrase*Node,
                // VoiceNode, SingNode, BarNode, ImportNode: out of scope
                // (see class summary). Skipped, not failed.
            }
        }

        var result = new Dictionary<string, List<NoteEvent>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in trackOrder)
            result[name] = tracks[name].Notes;

        return result;
    }

    private static void ExecuteStatements(TrackState track, IReadOnlyList<AstNode> body, ExecutionContext context)
    {
        foreach (var statement in body)
        {
            switch (statement)
            {
                case BpmNode bpm:
                    context.TempoMap.SetTempo(track.CurrentBeat, bpm.Bpm);
                    break;
                case TempoNode tempo:
                    context.TempoMap.SetTempo(track.CurrentBeat, tempo.Bpm);
                    break;
                case TempoRampNode ramp:
                    ScheduleTempoRamp(context, track.CurrentBeat, ramp);
                    break;
                case TimeSignatureNode time:
                    context.TimeSignatureNumerator = time.Numerator;
                    context.TimeSignatureDenominator = time.Denominator;
                    break;
                case VelocityNode velocity:
                    track.CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    track.CurrentDynamic = dynamic.Level;
                    break;
                case HumanizeNode humanize:
                    // v3: humanize is honored on the wave rail from here on —
                    // it applies to every note the track emits after the
                    // directive (same positional semantics as velocity/dynamic).
                    track.Humanize = humanize;
                    break;
                case RestNode rest:
                    AdvanceBeat(track, rest.Rest.DurationBeats);
                    break;
                case NoteNode note:
                    EmitNote(track, note, context);
                    break;
                case ChordNode chord:
                    EmitChord(track, chord, context);
                    break;
                case LoopNode loop:
                    ExecuteLoop(track, loop, context);
                    break;
                case PlayNode play:
                    ExecutePlay(track, play, context);
                    break;
                case SpeakNode speak:
                    EmitSpeech(track, speak, context);
                    break;

                // See class summary for the full list of intentionally-skipped node types.
            }
        }
    }

    private static void ExecutePlay(TrackState track, PlayNode play, ExecutionContext context)
    {
        if (context.Blocks.TryGetValue(play.SequenceName, out var blockBody))
        {
            ExecuteStatements(track, blockBody, context);
            return;
        }

        if (context.Sequences.TryGetValue(play.SequenceName, out var sequenceBody))
        {
            ExecuteStatements(track, sequenceBody, context);
            return;
        }

        if (context.PatternNames.Contains(play.SequenceName))
        {
            throw new NotSupportedException(
                $"Pattern playback ('{play.SequenceName}') is not implemented in SoundScript.Wave v1 " +
                "— arpeggio/strum pattern expansion is a documented v1 non-goal.");
        }

        throw new InvalidOperationException($"Unknown block '{play.SequenceName}'.");
    }

    private static void ExecuteLoop(TrackState track, LoopNode loop, ExecutionContext context)
    {
        for (var i = 0; i < loop.Count; i++)
            ExecuteStatements(track, loop.Body, context);
    }

    private static void EmitNote(TrackState track, NoteNode note, ExecutionContext context)
    {
        var startBeat = track.CurrentBeat;
        var durationBeats = note.DurationBeats;

        var (startSeconds, velocity) = ApplyHumanize(
            track,
            BeatsToSeconds(context, 0, startBeat),
            ResolveVelocity(track, note.Velocity));

        track.Notes.Add(new NoteEvent(
            FrequencyHz: MidiToHz(note.ToMidiNumber()),
            StartTimeSeconds: startSeconds,
            DurationSeconds: BeatsToSeconds(context, startBeat, durationBeats),
            Velocity: velocity,
            Timbre: TimbreParams.Default));

        AdvanceBeat(track, durationBeats);
    }

    private static void EmitChord(TrackState track, ChordNode chord, ExecutionContext context)
    {
        var startBeat = track.CurrentBeat;
        var durationBeats = chord.DurationBeats;

        var startSeconds = BeatsToSeconds(context, 0, startBeat);
        var durationSeconds = BeatsToSeconds(context, startBeat, durationBeats);
        var velocity = ResolveVelocity(track, chord.Velocity);

        // v1 stacks the chord's raw intervals — no voicing/balancing intelligence yet.
        foreach (var midiNumber in chord.ToMidiNumbers())
        {
            // Each chord tone jitters independently (its own note index) —
            // same policy as the MIDI backend's per-note HumanizeApplicator,
            // and what a human strum actually does.
            var (toneStart, toneVelocity) = ApplyHumanize(track, startSeconds, velocity);

            track.Notes.Add(new NoteEvent(
                FrequencyHz: MidiToHz(midiNumber),
                StartTimeSeconds: toneStart,
                DurationSeconds: durationSeconds,
                Velocity: toneVelocity,
                Timbre: TimbreParams.Default));
        }

        AdvanceBeat(track, durationBeats);
    }

    /// <summary>
    /// v3 seeded jitter (see the wave safeguards doc's determinism rules).
    /// Perturbs a note's start time and velocity inside the humanize bounds
    /// using the shared stateless PRNG, keyed by (seed, note index, salt):
    /// the explicit seed= if present, otherwise a stable hash of the track
    /// name (file content — never wall-clock, never unseeded Random).
    /// Timing/velocity bounds are resolved via <see cref="HumanizeNode.Resolve"/>,
    /// which keeps the bare-number-form vs. named-form fallback in one place.
    /// </summary>
    private static (double StartSeconds, double Velocity) ApplyHumanize(
        TrackState track, double startSeconds, double velocity)
    {
        var humanize = track.Humanize;
        if (humanize is null)
            return (startSeconds, velocity);

        var (timingSeconds, velocityAmount) = humanize.Resolve();
        if (timingSeconds <= 0 && velocityAmount <= 0)
            return (startSeconds, velocity);

        var seed = humanize.Seed ?? DeterministicRandom.DeriveSeed(track.Name.ToLowerInvariant());
        var noteIndex = track.Notes.Count;

        if (timingSeconds > 0)
        {
            startSeconds = Math.Max(0.0,
                startSeconds + DeterministicRandom.Unit(seed, noteIndex, TimingJitterSalt) * timingSeconds);
        }

        if (velocityAmount > 0)
        {
            velocity = Math.Clamp(
                velocity + DeterministicRandom.Unit(seed, noteIndex, VelocityJitterSalt) * velocityAmount,
                0.0, 1.0);
        }

        return (startSeconds, velocity);
    }

    // Formant-ish overtone ratio/level stacked onto a vowel's fundamental —
    // a crude second-formant echo, not a real formant filter bank.
    private const double VowelFormantRatio = 2.5;
    private const double VowelFormantVelocityScale = 0.35;

    private static readonly Adsr PlosiveEnvelope = new(Attack: 0.002, Decay: 0.015, Sustain: 0.0, Release: 0.01);
    private static readonly Adsr FricativeEnvelope = new(Attack: 0.01, Decay: 0.02, Sustain: 0.6, Release: 0.05);

    /// <summary>
    /// v3 prosody: expands a <c>speak</c> directive into a deterministic
    /// NoteEvent sequence on the default track, advancing its beat cursor so
    /// speech composes sequentially with any surrounding top-level notes.
    /// Beats convert to seconds through the same tempo map as everything else.
    ///
    /// Each phoneme's <see cref="PhonemeClass"/> picks its timbre (see
    /// PhonemeFrequencyTable for the class → band rationale): vowels stack a
    /// soft formant-ish overtone on the fundamental, nasals/liquids stay a
    /// plain tone, and plosives/fricatives synthesize from deterministic
    /// filtered noise (<see cref="OscillatorType.Noise"/>) instead of a tone.
    /// </summary>
    private static void EmitSpeech(TrackState track, SpeakNode speak, ExecutionContext context)
    {
        foreach (var tone in ProsodyToneGenerator.Generate(speak.Text, speak.Voice, speak.Seed))
        {
            if (!tone.IsRest)
            {
                var startBeat = track.CurrentBeat;
                var startSeconds = BeatsToSeconds(context, 0, startBeat);
                var durationSeconds = BeatsToSeconds(context, startBeat, tone.DurationBeats);

                foreach (var note in BuildSpeechNotes(tone, startSeconds, durationSeconds))
                    track.Notes.Add(note);
            }

            AdvanceBeat(track, tone.DurationBeats);
        }
    }

    private static IEnumerable<NoteEvent> BuildSpeechNotes(ProsodyTone tone, double startSeconds, double durationSeconds)
    {
        switch (tone.Class)
        {
            case PhonemeClass.Vowel:
                yield return new NoteEvent(
                    FrequencyHz: tone.FrequencyHz,
                    StartTimeSeconds: startSeconds,
                    DurationSeconds: durationSeconds,
                    Velocity: tone.Velocity,
                    Timbre: TimbreParams.Default with { Oscillator = OscillatorType.Triangle });
                yield return new NoteEvent(
                    FrequencyHz: tone.FrequencyHz * VowelFormantRatio,
                    StartTimeSeconds: startSeconds,
                    DurationSeconds: durationSeconds,
                    Velocity: tone.Velocity * VowelFormantVelocityScale,
                    Timbre: TimbreParams.Default);
                break;

            case PhonemeClass.Plosive:
                yield return new NoteEvent(
                    FrequencyHz: tone.FrequencyHz,
                    StartTimeSeconds: startSeconds,
                    DurationSeconds: durationSeconds,
                    Velocity: tone.Velocity,
                    Timbre: TimbreParams.Default with { Oscillator = OscillatorType.Noise, Envelope = PlosiveEnvelope });
                break;

            case PhonemeClass.Fricative:
                yield return new NoteEvent(
                    FrequencyHz: tone.FrequencyHz,
                    StartTimeSeconds: startSeconds,
                    DurationSeconds: durationSeconds,
                    Velocity: tone.Velocity,
                    Timbre: TimbreParams.Default with { Oscillator = OscillatorType.Noise, Envelope = FricativeEnvelope });
                break;

            case PhonemeClass.Nasal:
            case PhonemeClass.Liquid:
            default:
                yield return new NoteEvent(
                    FrequencyHz: tone.FrequencyHz,
                    StartTimeSeconds: startSeconds,
                    DurationSeconds: durationSeconds,
                    Velocity: tone.Velocity,
                    Timbre: TimbreParams.Default);
                break;
        }
    }

    // Distinct salts keep the two jitter streams (and prosody's PitchSalt=100)
    // uncorrelated even when they share a seed.
    private const int TimingJitterSalt = 1;
    private const int VelocityJitterSalt = 2;

    private static double ResolveVelocity(TrackState track, int? explicitVelocity)
    {
        var midiVelocity = explicitVelocity
            ?? track.CurrentDynamic?.ToVelocity()
            ?? track.CurrentVelocity;

        return Math.Clamp(midiVelocity / 127.0, 0.0, 1.0);
    }

    private static double BeatsToSeconds(ExecutionContext context, double startBeat, double durationBeats) =>
        context.TempoMap.BeatsToMilliseconds(startBeat, durationBeats) / 1000.0;

    private static void AdvanceBeat(TrackState track, double beats) =>
        track.CurrentBeat += Math.Max(0.0, beats);

    private static double MidiToHz(int midiNumber) =>
        440.0 * Math.Pow(2.0, (midiNumber - 69) / 12.0);

    private static double GetBeatsPerBar(ExecutionContext context)
    {
        var numerator = context.TimeSignatureNumerator ?? 4;
        var denominator = context.TimeSignatureDenominator ?? 4;
        return numerator * 4.0 / denominator;
    }

    private static void ScheduleTempoRamp(ExecutionContext context, double startBeat, TempoRampNode ramp)
    {
        var durationBeats = ramp.Bars * GetBeatsPerBar(context);
        context.TempoMap.AddRamp(startBeat, durationBeats, ramp.StartBpm, ramp.EndBpm);
    }

    /// <summary>
    /// Rejects a second top-level `track NAME { }`/`melody { }` declaration
    /// reusing a name already claimed (case-insensitively — track lookup is
    /// case-insensitive throughout). Without this, e.g. a `melody { }`
    /// shorthand block (which claims the reserved name "melody") followed by
    /// an unrelated `track melody { }` would silently resolve to the same
    /// TrackState, merging their beat cursor/velocity/humanize state instead
    /// of erroring.
    /// </summary>
    private static void RequireUniqueTrackName(HashSet<string> declaredTrackNames, string name)
    {
        if (!declaredTrackNames.Add(name))
        {
            throw new InvalidOperationException(
                $"Duplicate track name '{name}': a 'track {name} {{ }}' block or the 'melody {{ }}' " +
                "shorthand (which uses the reserved name 'melody') already declared this track in " +
                "this file. Track names must be unique.");
        }
    }

    private static TrackState GetOrCreateTrack(Dictionary<string, TrackState> tracks, List<string> order, string name)
    {
        if (tracks.TryGetValue(name, out var track))
            return track;

        track = new TrackState(name);
        tracks[name] = track;
        order.Add(name);
        return track;
    }

    private sealed class TrackState(string name)
    {
        /// <summary>Used to derive the default humanize seed (v3).</summary>
        public string Name { get; } = name;

        public double CurrentBeat { get; set; }
        public int CurrentVelocity { get; set; } = 64;
        public DynamicLevel? CurrentDynamic { get; set; }

        /// <summary>Active humanize directive (v3); null = no jitter.</summary>
        public HumanizeNode? Humanize { get; set; }

        public List<NoteEvent> Notes { get; } = [];
    }

    private sealed class ExecutionContext
    {
        public TempoAutomationMap TempoMap { get; } = new();
        public Dictionary<string, List<AstNode>> Sequences { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<AstNode>> Blocks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PatternNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int? TimeSignatureNumerator { get; set; }
        public int? TimeSignatureDenominator { get; set; }
    }
}
