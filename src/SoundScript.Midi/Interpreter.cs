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
        public double GlobalOffset { get; set; }
        public double CurrentBeat { get; set; }
        public int CurrentProgram { get; set; } = InstrumentMap.DefaultProgram;
        public string? CurrentInstrumentName { get; set; }
        public int CurrentVelocity { get; set; } = 64;
        public DynamicLevel? CurrentDynamic { get; set; }
        public bool HasBarLines { get; set; }
        public double CurrentMeasureBeats { get; set; }
        public List<double> MeasureBeats { get; } = [];
        public List<TimedNote> Notes { get; } = [];
        public List<ProgramChange> ProgramChanges { get; } = [];
        public int? LastEmittedMidi { get; set; }
        public int? LastPhraseMidi { get; set; }
        public bool PendingPhraseBoundary { get; set; }
        public int PhraseIndex { get; set; }
        public DynamicRampState? DynamicRamp { get; set; }
    }

    private sealed class ExecutionContext
    {
        public Dictionary<string, List<AstNode>> Blocks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<AstNode>> Sequences { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExpandingBlocks { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static InterpretedProgram Interpret(ProgramNode program)
    {
        var result = new InterpretedProgram();
        var context = new ExecutionContext();
        var tracks = new Dictionary<string, TrackBuilder>(StringComparer.OrdinalIgnoreCase);
        var clock = new GlobalBeatClock();
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
                case BlockNode block:
                    context.Blocks[block.Name] = block.Body;
                    break;
                case SequenceNode sequence:
                    context.Sequences[sequence.Name] = sequence.Body;
                    break;
                case TrackNode track:
                    tempo = ExecuteStatements(GetOrCreateTrack(tracks, track.Name), track.Body, context, tempo, result, clock);
                    result.Tempo = tempo;
                    break;
                case MelodyNode melody:
                    defaultTrack ??= GetOrCreateTrack(tracks, "melody");
                    tempo = ExecuteStatements(defaultTrack, melody.Body, context, tempo, result, clock);
                    result.Tempo = tempo;
                    break;
                case PlayNode play:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    tempo = ExecutePlay(defaultTrack, play, context, tempo, result, clock);
                    result.Tempo = tempo;
                    break;
                case LoopNode loop:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    tempo = ExecuteLoop(defaultTrack, loop, context, tempo, result, clock);
                    result.Tempo = tempo;
                    break;
                case InstrumentNode instrument:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    ApplyInstrument(defaultTrack, instrument);
                    break;
                case VelocityNode velocity:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    defaultTrack.CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    ApplyDynamic(defaultTrack, dynamic, result);
                    break;
                case RestNode rest:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitRest(defaultTrack, rest, clock, result);
                    break;
                case NoteNode note:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitNote(defaultTrack, note, tempo, clock, result);
                    break;
                case ChordNode chord:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitChord(defaultTrack, chord, tempo, clock, result);
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

    private static int ExecuteStatements(
        TrackBuilder track,
        IReadOnlyList<AstNode> body,
        ExecutionContext context,
        int tempo,
        InterpretedProgram result,
        GlobalBeatClock clock)
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
                    ApplyInstrument(track, instrument);
                    break;
                case VelocityNode velocity:
                    track.CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    ApplyDynamic(track, dynamic, result);
                    break;
                case RestNode rest:
                    EmitRest(track, rest, clock, result);
                    break;
                case NoteNode note:
                    EmitNote(track, note, tempo, clock, result);
                    break;
                case ChordNode chord:
                    EmitChord(track, chord, tempo, clock, result);
                    break;
                case LoopNode loop:
                    tempo = ExecuteLoop(track, loop, context, tempo, result, clock);
                    break;
                case PlayNode play:
                    tempo = ExecutePlay(track, play, context, tempo, result, clock);
                    break;
                case BarNode:
                    CloseMeasure(track);
                    break;
            }
        }

        return tempo;
    }

    private static int ExecutePlay(
        TrackBuilder track,
        PlayNode play,
        ExecutionContext context,
        int tempo,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        if (context.Blocks.TryGetValue(play.SequenceName, out var blockBody))
            return ExecuteNamedBlockPlay(track, play.SequenceName, blockBody, context, tempo, result, clock);

        if (context.Sequences.TryGetValue(play.SequenceName, out var sequenceBody))
            return ExecuteSequencePlay(track, play.SequenceName, sequenceBody, context, tempo, result, clock);

        throw new InvalidOperationException($"Unknown block '{play.SequenceName}'.");
    }

    private static int ExecuteNamedBlockPlay(
        TrackBuilder track,
        string blockName,
        IReadOnlyList<AstNode> blockBody,
        ExecutionContext context,
        int tempo,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        if (!context.ExpandingBlocks.Add(blockName))
            throw new InvalidOperationException($"Recursive block call detected: '{blockName}'.");

        try
        {
            var parentContext = CaptureContext(track);
            RestoreContext(track, parentContext);

            var tempoAfter = ExecuteStatements(track, blockBody, context, tempo, result, clock);
            track.LastPhraseMidi = track.LastEmittedMidi;
            track.PendingPhraseBoundary = track.LastPhraseMidi is not null;
            RestoreContext(track, parentContext);
            return tempoAfter;
        }
        finally
        {
            context.ExpandingBlocks.Remove(blockName);
        }
    }

    private static int ExecuteLoop(
        TrackBuilder track,
        LoopNode loop,
        ExecutionContext context,
        int tempo,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        var loopStart = BeatMath.RoundBeat(track.CurrentBeat);
        var iterationDuration = 0.0;

        for (var i = 0; i < loop.Count; i++)
        {
            if (i > 0)
                track.CurrentBeat = BeatMath.RoundBeat(loopStart + iterationDuration * i);

            var iterationStart = BeatMath.RoundBeat(track.CurrentBeat);
            tempo = ExecuteStatements(track, loop.Body, context, tempo, result, clock);
            iterationDuration = BeatMath.RoundBeat(track.CurrentBeat - iterationStart);
        }

        var alignedEnd = BeatMath.RoundBeat(loopStart + iterationDuration * loop.Count);
        var actualEnd = BeatMath.RoundBeat(track.CurrentBeat);
        if (Math.Abs(alignedEnd - actualEnd) > BeatMath.Epsilon)
        {
            AddWarning(result, "Loop alignment corrected");
            track.CurrentBeat = alignedEnd;
        }
        else
        {
            track.CurrentBeat = alignedEnd;
        }

        return tempo;
    }

    private static int ExecuteSequencePlay(
        TrackBuilder track,
        string sequenceName,
        IReadOnlyList<AstNode> sequenceBody,
        ExecutionContext context,
        int tempo,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        var parentContext = CaptureContext(track);

        if (!SequenceDefinesInstrument(sequenceBody) && parentContext.InstrumentName is not null)
            AddWarning(result, $"Sequence inherited instrument: {parentContext.InstrumentName}");

        RestoreContext(track, parentContext);

        var tempoAfter = ExecuteStatements(track, sequenceBody, context, tempo, result, clock);
        track.LastPhraseMidi = track.LastEmittedMidi;
        track.PendingPhraseBoundary = track.LastPhraseMidi is not null;
        RestoreContext(track, parentContext);
        return tempoAfter;
    }

    private static void ApplyDynamic(TrackBuilder track, DynamicNode dynamic, InterpretedProgram result)
    {
        if (DynamicContext.IsAbruptChange(track.CurrentDynamic, dynamic.Level))
        {
            track.DynamicRamp = DynamicContext.StartRamp(track.CurrentDynamic, dynamic.Level);
            AddWarning(result, "Dynamic ramp applied");
        }

        track.CurrentDynamic = dynamic.Level;
    }

    private static void ApplyInstrument(TrackBuilder track, InstrumentNode instrument)
    {
        if (track.CurrentProgram != instrument.ProgramNumber)
        {
            track.ProgramChanges.Add(new ProgramChange(
                BeatMath.RoundBeat(track.CurrentBeat + track.GlobalOffset),
                instrument.ProgramNumber));
            track.CurrentProgram = instrument.ProgramNumber;
        }

        track.CurrentInstrumentName = instrument.Name;
    }

    private static void EmitRest(TrackBuilder track, RestNode rest, GlobalBeatClock clock, InterpretedProgram result)
    {
        var globalBeat = clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset);
        rest.Rest.StartTime = globalBeat;
        MaybeApplySyncCorrection(track, globalBeat, clock, result);
        AdvanceTiming(track, rest.Rest.DurationBeats);
    }

    private static void EmitNote(TrackBuilder track, NoteNode note, int tempo, GlobalBeatClock clock, InterpretedProgram result)
    {
        var notation = ApplyMusicalIntelligence(track, note.Notation, result);
        var globalBeat = clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset);
        notation.StartTime = globalBeat;
        MaybeApplySyncCorrection(track, globalBeat, clock, result);

        var (rampVelocity, _) = DynamicContext.Resolve(track.DynamicRamp);
        var shaped = PlaybackShaper.ShapeNote(
            note.Velocity,
            rampVelocity,
            notation.Dynamic,
            track.CurrentDynamic,
            track.CurrentVelocity,
            notation.Articulation,
            track.CurrentInstrumentName,
            notation.DurationBeats);

        ApplyPlaybackWarnings(result, shaped);

        notation = notation with
        {
            ShapedVelocity = shaped.Velocity,
            ShapedDurationBeats = shaped.DurationBeats
        };

        var durationMs = BeatsToMilliseconds(shaped.DurationBeats, tempo);
        var midiNumber = notation.ResolvedMidiNumber;

        track.Notes.Add(new TimedNote(
            midiNumber,
            globalBeat,
            shaped.DurationBeats,
            durationMs,
            shaped.Velocity));

        track.LastEmittedMidi = midiNumber;
        AdvanceTiming(track, notation.DurationBeats);
    }

    private static NotatedNote ApplyMusicalIntelligence(
        TrackBuilder track,
        NotatedNote notation,
        InterpretedProgram result)
    {
        notation = notation with { PhraseIndex = ++track.PhraseIndex };

        if (track.PendingPhraseBoundary && track.LastPhraseMidi is not null)
        {
            var (phraseAdjusted, phraseChanged) = PhraseSmoother.Apply(track.LastPhraseMidi, notation);
            notation = phraseAdjusted;
            if (phraseChanged)
                AddWarning(result, "Phrase smoothing applied");
            track.PendingPhraseBoundary = false;
        }

        var previousMidi = track.LastEmittedMidi;

        var (octaveAdjusted, octaveChanged) = OctaveSmoother.Apply(previousMidi, notation);
        notation = octaveAdjusted;
        if (octaveChanged)
            AddWarning(result, "Octave smoothing applied");

        var (contourAdjusted, contourChanged) = MelodicContour.Apply(previousMidi, notation);
        notation = contourAdjusted;
        if (contourChanged)
            AddWarning(result, "Melodic contour correction applied");

        return notation;
    }

    private static void EmitChord(TrackBuilder track, ChordNode chord, int tempo, GlobalBeatClock clock, InterpretedProgram result)
    {
        var shaped = PlaybackShaper.ShapeChordVelocity(
            chord.Velocity,
            track.CurrentDynamic,
            track.CurrentVelocity,
            track.CurrentInstrumentName);

        ApplyPlaybackWarnings(result, shaped);

        var globalBeat = clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset);
        MaybeApplySyncCorrection(track, globalBeat, clock, result);

        var (voicedNotes, voicedAdjusted) = ChordVoicing.Apply(chord.ToMidiNumbers());
        if (voicedAdjusted)
            AddWarning(result, "Chord voicing adjustment applied");

        var (spacedNotes, spacedAdjusted) = HarmonicSpacing.Apply(voicedNotes);
        if (spacedAdjusted)
            AddWarning(result, "Harmonic spacing adjustment applied");

        var (balancedVelocities, balanced) = ChordBalancer.Apply(spacedNotes, shaped.Velocity);
        if (balanced)
            AddWarning(result, "Chord balance applied");

        var durationMs = BeatsToMilliseconds(chord.DurationBeats, tempo);
        var durationBeats = BeatMath.RoundBeat(chord.DurationBeats);

        for (var i = 0; i < spacedNotes.Length; i++)
        {
            track.Notes.Add(new TimedNote(
                spacedNotes[i],
                globalBeat,
                durationBeats,
                durationMs,
                balancedVelocities[i]));
        }

        AdvanceTiming(track, chord.DurationBeats);
    }

    private static void ApplyPlaybackWarnings(InterpretedProgram result, PlaybackShapeResult shaped)
    {
        if (shaped.DynamicShaped)
            AddWarning(result, "Dynamic shaping applied");
        if (shaped.ArticulationShaped)
            AddWarning(result, "Articulation shaping applied");
        if (shaped.GainRefined)
            AddWarning(result, "Instrument gain refinement applied");
        if (shaped.DurationNormalized)
            AddWarning(result, "Duration normalization applied");
        if (shaped.ExpressiveApplied)
            AddWarning(result, "Expressive curve applied");
    }

    private static void AdvanceTiming(TrackBuilder track, double beats)
    {
        track.CurrentBeat = BeatMath.AddBeats(track.CurrentBeat, beats);
        track.CurrentMeasureBeats = BeatMath.AddBeats(track.CurrentMeasureBeats, beats);
    }

    private static void CloseMeasure(TrackBuilder track)
    {
        track.HasBarLines = true;
        track.MeasureBeats.Add(BeatMath.RoundBeat(track.CurrentMeasureBeats));
        track.CurrentMeasureBeats = 0;
    }

    private static void FlushMeasureValidation(TrackBuilder track, InterpretedProgram result)
    {
        if (!track.HasBarLines
            || result.TimeSignatureNumerator is not int numerator
            || result.TimeSignatureDenominator is not int denominator)
            return;

        if (track.CurrentMeasureBeats > 0)
            track.MeasureBeats.Add(BeatMath.RoundBeat(track.CurrentMeasureBeats));

        foreach (var warning in NotationParser.ValidateMeasure(track.MeasureBeats, numerator, denominator))
            AddWarning(result, warning);
    }

    private static SequenceContext CaptureContext(TrackBuilder track) => new()
    {
        ProgramNumber = track.CurrentProgram,
        InstrumentName = track.CurrentInstrumentName,
        Velocity = track.CurrentVelocity,
        Dynamic = track.CurrentDynamic
    };

    private static void RestoreContext(TrackBuilder track, SequenceContext context)
    {
        if (track.CurrentProgram != context.ProgramNumber)
        {
            track.ProgramChanges.Add(new ProgramChange(
                BeatMath.RoundBeat(track.CurrentBeat + track.GlobalOffset),
                context.ProgramNumber));
            track.CurrentProgram = context.ProgramNumber;
        }

        track.CurrentInstrumentName = context.InstrumentName;
        track.CurrentVelocity = context.Velocity;
        track.CurrentDynamic = context.Dynamic;
    }

    private static bool SequenceDefinesInstrument(IReadOnlyList<AstNode> body) =>
        body.Any(statement => statement is InstrumentNode);

    private static void MaybeApplySyncCorrection(
        TrackBuilder track,
        double globalBeat,
        GlobalBeatClock clock,
        InterpretedProgram result)
    {
        var normalized = clock.NormalizeTrackBeat(globalBeat, track.GlobalOffset);
        if (Math.Abs(normalized - track.CurrentBeat) <= BeatMath.Epsilon)
            return;

        track.CurrentBeat = normalized;
        if (clock.MarkSyncCorrection())
            AddWarning(result, "Global sync correction applied");
    }

    private static void AddWarning(InterpretedProgram result, string warning)
    {
        if (!result.Warnings.Contains(warning))
            result.Warnings.Add(warning);
    }

    public static double BeatsToMilliseconds(double beats, int bpm) =>
        (60_000.0 / bpm) * beats;
}
