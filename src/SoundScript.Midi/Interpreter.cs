using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Parser;

namespace SoundScript.Midi;

public static class Interpreter
{
    private sealed class TrackBuilder
    {
        public string Name { get; init; } = "default";
        public double CurrentBeat { get; set; }
        public int CurrentProgram { get; set; } = InstrumentMap.DefaultProgram;
        public int CurrentVelocity { get; set; } = 64;
        public DynamicLevel? CurrentDynamic { get; set; }
        public bool HasBarLines { get; set; }
        public double CurrentMeasureBeats { get; set; }
        public List<double> MeasureBeats { get; } = [];
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
                    tempo = ExecuteBlock(GetOrCreateTrack(tracks, track.Name), track.Body, sequences, tempo, result);
                    result.Tempo = tempo;
                    break;
                case MelodyNode melody:
                    defaultTrack ??= GetOrCreateTrack(tracks, "melody");
                    tempo = ExecuteBlock(defaultTrack, melody.Body, sequences, tempo, result);
                    result.Tempo = tempo;
                    break;
                case PlayNode play:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    if (!sequences.TryGetValue(play.SequenceName, out var sequenceBody))
                        throw new InvalidOperationException($"Unknown sequence '{play.SequenceName}'.");

                    tempo = ExecuteBlock(defaultTrack, sequenceBody, sequences, tempo, result);
                    result.Tempo = tempo;
                    break;
                case LoopNode loop:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    for (var i = 0; i < loop.Count; i++)
                        tempo = ExecuteBlock(defaultTrack, loop.Body, sequences, tempo, result);
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
                case DynamicNode dynamic:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    defaultTrack.CurrentDynamic = dynamic.Level;
                    break;
                case RestNode rest:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitRest(defaultTrack, rest);
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
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    CloseMeasure(defaultTrack);
                    break;
            }
        }

        foreach (var track in tracks.Values)
        {
            FlushMeasureValidation(track, result);
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
        int tempo,
        InterpretedProgram result)
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
                case TimeSignatureNode time:
                    result.TimeSignatureNumerator = time.Numerator;
                    result.TimeSignatureDenominator = time.Denominator;
                    break;
                case InstrumentNode instrument:
                    ApplyInstrument(track, instrument.ProgramNumber);
                    break;
                case VelocityNode velocity:
                    track.CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    track.CurrentDynamic = dynamic.Level;
                    break;
                case RestNode rest:
                    EmitRest(track, rest);
                    break;
                case NoteNode note:
                    EmitNote(track, note, tempo);
                    break;
                case ChordNode chord:
                    EmitChord(track, chord, tempo);
                    break;
                case LoopNode loop:
                    for (var i = 0; i < loop.Count; i++)
                        tempo = ExecuteBlock(track, loop.Body, sequences, tempo, result);
                    break;
                case PlayNode play:
                    if (!sequences.TryGetValue(play.SequenceName, out var sequenceBody))
                        throw new InvalidOperationException($"Unknown sequence '{play.SequenceName}'.");
                    tempo = ExecuteBlock(track, sequenceBody, sequences, tempo, result);
                    break;
                case BarNode:
                    CloseMeasure(track);
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

    private static void EmitRest(TrackBuilder track, RestNode rest)
    {
        rest.Rest.StartTime = track.CurrentBeat;
        AdvanceTiming(track, rest.Rest.DurationBeats);
    }

    private static void EmitNote(TrackBuilder track, NoteNode note, int tempo)
    {
        var notation = note.Notation;
        notation.StartTime = track.CurrentBeat;

        var velocity = note.Velocity
            ?? notation.Dynamic?.ToVelocity()
            ?? track.CurrentDynamic?.ToVelocity()
            ?? track.CurrentVelocity;

        velocity = ApplyArticulationVelocity(velocity, notation.Articulation);

        var writtenBeats = notation.DurationBeats;
        var playbackBeats = ApplyArticulationDuration(writtenBeats, notation.Articulation);
        var durationMs = BeatsToMilliseconds(playbackBeats, tempo);

        track.Notes.Add(new TimedNote(
            notation.ToMidiNumber(),
            notation.StartTime,
            playbackBeats,
            durationMs,
            velocity));

        AdvanceTiming(track, writtenBeats);
    }

    private static void EmitChord(TrackBuilder track, ChordNode chord, int tempo)
    {
        var velocity = chord.Velocity ?? track.CurrentDynamic?.ToVelocity() ?? track.CurrentVelocity;
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

        AdvanceTiming(track, chord.DurationBeats);
    }

    private static void AdvanceTiming(TrackBuilder track, double beats)
    {
        track.CurrentBeat += beats;
        track.CurrentMeasureBeats += beats;
    }

    private static void CloseMeasure(TrackBuilder track)
    {
        track.HasBarLines = true;
        track.MeasureBeats.Add(track.CurrentMeasureBeats);
        track.CurrentMeasureBeats = 0;
    }

    private static void FlushMeasureValidation(TrackBuilder track, InterpretedProgram result)
    {
        if (!track.HasBarLines
            || result.TimeSignatureNumerator is not int numerator
            || result.TimeSignatureDenominator is not int denominator)
            return;

        if (track.CurrentMeasureBeats > 0)
            track.MeasureBeats.Add(track.CurrentMeasureBeats);

        foreach (var warning in NotationParser.ValidateMeasure(track.MeasureBeats, numerator, denominator))
        {
            if (!result.Warnings.Contains(warning))
                result.Warnings.Add(warning);
        }
    }

    private static double ApplyArticulationDuration(double beats, ArticulationType? articulation) =>
        articulation switch
        {
            ArticulationType.Staccato => beats * 0.5,
            ArticulationType.Legato => beats,
            ArticulationType.Accent => beats,
            _ => beats
        };

    private static int ApplyArticulationVelocity(int velocity, ArticulationType? articulation) =>
        articulation switch
        {
            ArticulationType.Accent => Math.Min(127, (int)Math.Round(velocity * 1.25)),
            ArticulationType.Legato => Math.Max(1, (int)Math.Round(velocity * 0.95)),
            _ => velocity
        };

    public static double BeatsToMilliseconds(double beats, int bpm) =>
        (60_000.0 / bpm) * beats;
}
