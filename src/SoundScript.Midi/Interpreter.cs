using SoundScript.Core;
using SoundScript.Core.Ast;

namespace SoundScript.Midi;

public static class Interpreter
{
    private sealed class TrackBuilder
    {
        public string Name { get; init; } = "default";
        public double CurrentBeat { get; set; }
        public int CurrentProgram { get; set; } = InstrumentMap.DefaultProgram;
        public int CurrentVelocity { get; set; } = 64;
        public List<TimedNote> Notes { get; } = [];
        public List<ProgramChange> ProgramChanges { get; } = [];
    }

    public static InterpretedProgram Interpret(ProgramNode program)
    {
        var result = new InterpretedProgram();
        var sequences = new Dictionary<string, List<AstNode>>(StringComparer.OrdinalIgnoreCase);
        var tracks = new Dictionary<string, TrackBuilder>(StringComparer.OrdinalIgnoreCase);
        TrackBuilder? defaultTrack = null;
        var tempo = result.Tempo;

        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case TempoNode tempoNode:
                    tempo = tempoNode.Bpm;
                    result.Tempo = tempo;
                    break;
                case BpmNode bpm:
                    tempo = bpm.Bpm;
                    result.Tempo = tempo;
                    break;
                case TimeSignatureNode time:
                    result.TimeSignatureNumerator = time.Numerator;
                    result.TimeSignatureDenominator = time.Denominator;
                    break;
                case SequenceNode sequence:
                    sequences[sequence.Name] = sequence.Body;
                    break;
                case TrackNode track:
                    tempo = ExecuteBlock(GetOrCreateTrack(tracks, track.Name), track.Body, sequences, tempo);
                    result.Tempo = tempo;
                    break;
                case MelodyNode melody:
                    defaultTrack ??= GetOrCreateTrack(tracks, "melody");
                    tempo = ExecuteBlock(defaultTrack, melody.Body, sequences, tempo);
                    result.Tempo = tempo;
                    break;
                case PlayNode play:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    if (!sequences.TryGetValue(play.SequenceName, out var sequenceBody))
                        throw new InvalidOperationException($"Unknown sequence '{play.SequenceName}'.");

                    tempo = ExecuteBlock(defaultTrack, sequenceBody, sequences, tempo);
                    result.Tempo = tempo;
                    break;
                case LoopNode loop:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    for (var i = 0; i < loop.Count; i++)
                        tempo = ExecuteBlock(defaultTrack, loop.Body, sequences, tempo);
                    result.Tempo = tempo;
                    break;
                case InstrumentNode instrument:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    ApplyInstrument(defaultTrack, instrument.ProgramNumber);
                    break;
                case VelocityNode velocity:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    defaultTrack.CurrentVelocity = velocity.Velocity;
                    break;
                case NoteNode note:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitNote(defaultTrack, note, tempo);
                    break;
                case ChordNode chord:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitChord(defaultTrack, chord, tempo);
                    break;
                case BarNode:
                    break;
            }
        }

        foreach (var track in tracks.Values)
        {
            if (track.Notes.Count == 0)
                continue;

            var interpretedTrack = new InterpretedTrack { Name = track.Name };
            interpretedTrack.Notes.AddRange(track.Notes);
            interpretedTrack.ProgramChanges.AddRange(track.ProgramChanges);
            result.Tracks.Add(interpretedTrack);
        }

        return result;
    }

    private static TrackBuilder GetOrCreateTrack(Dictionary<string, TrackBuilder> tracks, string name)
    {
        if (!tracks.TryGetValue(name, out var track))
        {
            track = new TrackBuilder { Name = name };
            tracks[name] = track;
        }

        return track;
    }

    private static int ExecuteBlock(
        TrackBuilder track,
        IReadOnlyList<AstNode> body,
        Dictionary<string, List<AstNode>> sequences,
        int tempo)
    {
        foreach (var statement in body)
        {
            switch (statement)
            {
                case BpmNode bpm:
                    tempo = bpm.Bpm;
                    break;
                case TempoNode tempoNode:
                    tempo = tempoNode.Bpm;
                    break;
                case InstrumentNode instrument:
                    ApplyInstrument(track, instrument.ProgramNumber);
                    break;
                case VelocityNode velocity:
                    track.CurrentVelocity = velocity.Velocity;
                    break;
                case NoteNode note:
                    EmitNote(track, note, tempo);
                    break;
                case ChordNode chord:
                    EmitChord(track, chord, tempo);
                    break;
                case LoopNode loop:
                    for (var i = 0; i < loop.Count; i++)
                        tempo = ExecuteBlock(track, loop.Body, sequences, tempo);
                    break;
                case PlayNode play:
                    if (!sequences.TryGetValue(play.SequenceName, out var sequenceBody))
                        throw new InvalidOperationException($"Unknown sequence '{play.SequenceName}'.");
                    tempo = ExecuteBlock(track, sequenceBody, sequences, tempo);
                    break;
                case BarNode:
                    break;
            }
        }

        return tempo;
    }

    private static void ApplyInstrument(TrackBuilder track, int programNumber)
    {
        if (track.CurrentProgram != programNumber)
        {
            track.ProgramChanges.Add(new ProgramChange(track.CurrentBeat, programNumber));
            track.CurrentProgram = programNumber;
        }
    }

    private static void EmitNote(TrackBuilder track, NoteNode note, int tempo)
    {
        var velocity = note.Velocity ?? track.CurrentVelocity;
        var notation = note.Notation;
        notation.StartTime = track.CurrentBeat;

        var durationMs = BeatsToMilliseconds(notation.DurationBeats, tempo);

        track.Notes.Add(new TimedNote(
            notation.ToMidiNumber(),
            notation.StartTime,
            notation.DurationBeats,
            durationMs,
            velocity));

        track.CurrentBeat += notation.DurationBeats;
    }

    private static void EmitChord(TrackBuilder track, ChordNode chord, int tempo)
    {
        var velocity = chord.Velocity ?? track.CurrentVelocity;
        var durationMs = BeatsToMilliseconds(chord.DurationBeats, tempo);
        var startBeat = track.CurrentBeat;

        foreach (var midiNumber in chord.ToMidiNumbers())
        {
            track.Notes.Add(new TimedNote(
                midiNumber,
                startBeat,
                chord.DurationBeats,
                durationMs,
                velocity));
        }

        track.CurrentBeat += chord.DurationBeats;
    }

    public static double BeatsToMilliseconds(double beats, int bpm) =>
        (60_000.0 / bpm) * beats;
}
