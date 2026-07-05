// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Wave.Model;

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
/// gain, humanize, orchestration, phrase shaping, chord voicing/balancing
/// intelligence, arpeggio/strum patterns, and vocal (voice/sing) tracks —
/// are not yet implemented. They are silently skipped rather than failing,
/// so a MIDI-only .ss file can still be pushed through this rail and
/// produce a flat, default-timbre rendering instead of an error. Every
/// track gets <see cref="TimbreParams.Default"/> (sine + neutral ADSR)
/// since the grammar has no per-track timbre directive yet.
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
                    ExecuteStatements(GetOrCreateTrack(tracks, trackOrder, track.Name), track.Body, context);
                    break;
                case MelodyNode melody:
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

                // InstrumentNode, GainNode, HumanizeNode, OrchestrationNode,
                // Phrase*Node, VoiceNode, SingNode, BarNode, ImportNode: out
                // of scope for v1 (see class summary). Skipped, not failed.
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

        track.Notes.Add(new NoteEvent(
            FrequencyHz: MidiToHz(note.ToMidiNumber()),
            StartTimeSeconds: BeatsToSeconds(context, 0, startBeat),
            DurationSeconds: BeatsToSeconds(context, startBeat, durationBeats),
            Velocity: ResolveVelocity(track, note.Velocity),
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
            track.Notes.Add(new NoteEvent(
                FrequencyHz: MidiToHz(midiNumber),
                StartTimeSeconds: startSeconds,
                DurationSeconds: durationSeconds,
                Velocity: velocity,
                Timbre: TimbreParams.Default));
        }

        AdvanceBeat(track, durationBeats);
    }

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

    private static TrackState GetOrCreateTrack(Dictionary<string, TrackState> tracks, List<string> order, string name)
    {
        if (tracks.TryGetValue(name, out var track))
            return track;

        track = new TrackState();
        tracks[name] = track;
        order.Add(name);
        return track;
    }

    private sealed class TrackState
    {
        public double CurrentBeat { get; set; }
        public int CurrentVelocity { get; set; } = 64;
        public DynamicLevel? CurrentDynamic { get; set; }
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
