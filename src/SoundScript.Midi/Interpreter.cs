using SoundScript.Core;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Parser;

namespace SoundScript.Midi;

public static class Interpreter
{
    private sealed class TrackLayer
    {
        public string? InstrumentName { get; init; }
        public int ProgramNumber { get; init; }
        public byte Channel { get; init; }
    }

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
        public List<TrackLayer> Layers { get; } = [];
        public int? LastEmittedMidi { get; set; }
        public int? LastPhraseMidi { get; set; }
        public bool PendingPhraseBoundary { get; set; }
        public int PhraseIndex { get; set; }
        public DynamicRampState? DynamicRamp { get; set; }
        public double Gain { get; set; } = 1.0;
        public double Humanize { get; set; }
        public PhraseScope? ActivePhrase { get; set; }
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
        var globalTempoBeat = 0.0;

        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case TempoNode tempoNode:
                    ScheduleTempo(result, globalTempoBeat, tempoNode.Bpm);
                    break;
                case TempoRampNode tempoRamp:
                    ScheduleTempoRamp(result, globalTempoBeat, tempoRamp);
                    globalTempoBeat += tempoRamp.Bars * GetBeatsPerBar(result);
                    break;
                case BpmNode bpm:
                    ScheduleTempo(result, globalTempoBeat, bpm.Bpm);
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
                    ExecuteStatements(GetOrCreateTrack(tracks, track.Name), track.Body, context, result, clock);
                    break;
                case MelodyNode melody:
                    defaultTrack ??= GetOrCreateTrack(tracks, "melody");
                    ExecuteStatements(defaultTrack, melody.Body, context, result, clock);
                    break;
                case PlayNode play:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    ExecutePlay(defaultTrack, play, context, result, clock);
                    break;
                case LoopNode loop:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    ExecuteLoop(defaultTrack, loop, context, result, clock);
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
                    EmitNote(defaultTrack, note, clock, result);
                    break;
                case ChordNode chord:
                    defaultTrack ??= GetOrCreateTrack(tracks, "default");
                    EmitChord(defaultTrack, chord, clock, result);
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
            var noteIndex = 0;
            foreach (var note in track.Notes)
            {
                var startBeat = HumanizeApplicator.ApplyToStartBeat(
                    note.StartBeat,
                    track.Humanize,
                    (int)Math.Round(result.TempoMap.GetBpmAt(note.StartBeat)),
                    noteIndex,
                    note.Channel);
                var velocity = HumanizeApplicator.ApplyVelocity(
                    note.Velocity,
                    track.Humanize,
                    noteIndex,
                    note.Channel);
                interpretedTrack.Notes.Add(note with { StartBeat = startBeat, Velocity = velocity });
                noteIndex++;
            }

            if (track.Layers.Count > 0)
            {
                foreach (var layer in track.Layers)
                {
                    interpretedTrack.ProgramChanges.Add(new ProgramChange(0, layer.ProgramNumber, layer.Channel));
                }
            }
            else
            {
                interpretedTrack.ProgramChanges.AddRange(track.ProgramChanges);
            }

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

    private static void ExecuteStatements(
        TrackBuilder track,
        IReadOnlyList<AstNode> body,
        ExecutionContext context,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        foreach (var statement in body)
        {
            switch (statement)
            {
                case BpmNode bpm:
                    ScheduleTempo(result, clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset), bpm.Bpm);
                    break;
                case TempoNode tempoNode:
                    ScheduleTempo(result, clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset), tempoNode.Bpm);
                    break;
                case TempoRampNode tempoRamp:
                    ScheduleTempoRamp(result, clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset), tempoRamp);
                    break;
                case TimeSignatureNode time:
                    result.TimeSignatureNumerator = time.Numerator;
                    result.TimeSignatureDenominator = time.Denominator;
                    break;
                case InstrumentNode instrument:
                    ApplyInstrument(track, instrument);
                    break;
                case LayerNode layer:
                    AddLayer(track, layer);
                    break;
                case GainNode gain:
                    track.Gain = gain.Value;
                    break;
                case HumanizeNode humanize:
                    track.Humanize = humanize.Value;
                    break;
                case VelocityNode velocity:
                    track.CurrentVelocity = velocity.Velocity;
                    break;
                case DynamicNode dynamic:
                    ApplyDynamic(track, dynamic, result);
                    break;
                case PhraseCurveNode curve:
                    ApplyPhraseCurve(track, curve);
                    break;
                case PhraseTransitionNode transition:
                    ApplyPhraseTransition(track, transition);
                    break;
                case RestNode rest:
                    EmitRest(track, rest, clock, result);
                    break;
                case NoteNode note:
                    EmitNote(track, note, clock, result);
                    break;
                case ChordNode chord:
                    EmitChord(track, chord, clock, result);
                    break;
                case LoopNode loop:
                    ExecuteLoop(track, loop, context, result, clock);
                    break;
                case PhraseNode phrase:
                    ExecutePhrase(track, phrase, context, result, clock);
                    break;
                case PlayNode play:
                    ExecutePlay(track, play, context, result, clock);
                    break;
                case BarNode:
                    CloseMeasure(track);
                    break;
            }
        }
    }

    private static void ExecutePlay(
        TrackBuilder track,
        PlayNode play,
        ExecutionContext context,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        if (context.Blocks.TryGetValue(play.SequenceName, out var blockBody))
        {
            ExecuteNamedBlockPlay(track, play.SequenceName, blockBody, context, result, clock);
            return;
        }

        if (context.Sequences.TryGetValue(play.SequenceName, out var sequenceBody))
        {
            ExecuteSequencePlay(track, play.SequenceName, sequenceBody, context, result, clock);
            return;
        }

        throw new InvalidOperationException($"Unknown block '{play.SequenceName}'.");
    }

    private static void ExecuteNamedBlockPlay(
        TrackBuilder track,
        string blockName,
        IReadOnlyList<AstNode> blockBody,
        ExecutionContext context,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        if (!context.ExpandingBlocks.Add(blockName))
            throw new InvalidOperationException($"Recursive block call detected: '{blockName}'.");

        try
        {
            var parentContext = CaptureContext(track);
            RestoreContext(track, parentContext);

            ExecuteStatements(track, blockBody, context, result, clock);
            track.LastPhraseMidi = track.LastEmittedMidi;
            track.PendingPhraseBoundary = track.LastPhraseMidi is not null;
            RestoreContext(track, parentContext);
        }
        finally
        {
            context.ExpandingBlocks.Remove(blockName);
        }
    }

    private static void ExecutePhrase(
        TrackBuilder track,
        PhraseNode phrase,
        ExecutionContext context,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        var parentContext = CaptureContext(track);
        var phraseScope = new PhraseScope
        {
            Dynamic = track.CurrentDynamic,
            NoteCount = CountPhraseNotes(phrase.Body, context)
        };
        track.ActivePhrase = phraseScope;

        ExecuteStatements(track, phrase.Body, context, result, clock);

        track.LastPhraseMidi = track.LastEmittedMidi;
        track.PendingPhraseBoundary = track.LastPhraseMidi is not null;
        track.ActivePhrase = null;
        RestoreContext(track, parentContext);
    }

    private static int CountPhraseNotes(IReadOnlyList<AstNode> body, ExecutionContext context)
    {
        var count = 0;

        foreach (var statement in body)
        {
            switch (statement)
            {
                case NoteNode:
                case ChordNode:
                    count++;
                    break;
                case PlayNode play when context.Blocks.TryGetValue(play.SequenceName, out var blockBody):
                    count += CountPhraseNotes(blockBody, context);
                    break;
                case PlayNode play when context.Sequences.TryGetValue(play.SequenceName, out var sequenceBody):
                    count += CountPhraseNotes(sequenceBody, context);
                    break;
                case LoopNode loop:
                    count += CountPhraseNotes(loop.Body, context) * loop.Count;
                    break;
                case PhraseNode nested:
                    count += CountPhraseNotes(nested.Body, context);
                    break;
            }
        }

        return count;
    }

    private static void ExecuteLoop(
        TrackBuilder track,
        LoopNode loop,
        ExecutionContext context,
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
            ExecuteStatements(track, loop.Body, context, result, clock);
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
    }

    private static void ExecuteSequencePlay(
        TrackBuilder track,
        string sequenceName,
        IReadOnlyList<AstNode> sequenceBody,
        ExecutionContext context,
        InterpretedProgram result,
        GlobalBeatClock clock)
    {
        var parentContext = CaptureContext(track);

        if (!SequenceDefinesInstrument(sequenceBody) && parentContext.InstrumentName is not null)
            AddWarning(result, $"Sequence inherited instrument: {parentContext.InstrumentName}");

        RestoreContext(track, parentContext);

        ExecuteStatements(track, sequenceBody, context, result, clock);
        track.LastPhraseMidi = track.LastEmittedMidi;
        track.PendingPhraseBoundary = track.LastPhraseMidi is not null;
        RestoreContext(track, parentContext);
    }

    private static double GetBeatsPerBar(InterpretedProgram result)
    {
        var numerator = result.TimeSignatureNumerator ?? 4;
        var denominator = result.TimeSignatureDenominator ?? 4;
        return numerator * 4.0 / denominator;
    }

    private static void ScheduleTempo(InterpretedProgram result, double beat, int bpm)
    {
        result.TempoMap.SetTempo(beat, bpm);
        result.Tempo = bpm;
    }

    private static void ScheduleTempoRamp(InterpretedProgram result, double beat, TempoRampNode ramp)
    {
        var durationBeats = ramp.Bars * GetBeatsPerBar(result);
        result.TempoMap.AddRamp(beat, durationBeats, ramp.StartBpm, ramp.EndBpm);
        result.Tempo = ramp.EndBpm;
    }

    private static void ApplyDynamic(TrackBuilder track, DynamicNode dynamic, InterpretedProgram result)
    {
        if (DynamicContext.IsAbruptChange(track.CurrentDynamic, dynamic.Level))
        {
            track.DynamicRamp = DynamicContext.StartRamp(track.CurrentDynamic, dynamic.Level);
            AddWarning(result, "Dynamic ramp applied");
        }

        track.CurrentDynamic = dynamic.Level;
        if (track.ActivePhrase is not null)
            track.ActivePhrase.Dynamic = dynamic.Level;
    }

    private static void ApplyPhraseCurve(TrackBuilder track, PhraseCurveNode curve)
    {
        if (track.ActivePhrase is not null)
            track.ActivePhrase.Curve = curve.Curve;
    }

    private static void ApplyPhraseTransition(TrackBuilder track, PhraseTransitionNode transition)
    {
        if (track.ActivePhrase is not null)
            track.ActivePhrase.Transition = transition.Mode;
    }

    private static void AddLayer(TrackBuilder track, LayerNode layer)
    {
        track.Layers.Add(new TrackLayer
        {
            InstrumentName = layer.Name,
            ProgramNumber = layer.ProgramNumber,
            Channel = (byte)track.Layers.Count
        });
    }

    private static IEnumerable<TrackLayer> GetPlaybackLayers(TrackBuilder track)
    {
        if (track.Layers.Count > 0)
            return track.Layers;

        return
        [
            new TrackLayer
            {
                InstrumentName = track.CurrentInstrumentName,
                ProgramNumber = track.CurrentProgram,
                Channel = 0
            }
        ];
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

    private static void EmitNote(TrackBuilder track, NoteNode note, GlobalBeatClock clock, InterpretedProgram result)
    {
        var notation = ApplyMusicalIntelligence(track, note.Notation, result);
        var globalBeat = clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset);
        notation.StartTime = globalBeat;
        MaybeApplySyncCorrection(track, globalBeat, clock, result);

        var (rampVelocity, _) = DynamicContext.Resolve(track.DynamicRamp);
        var (resolvedNoteVelocity, resolvedRampVelocity, effectiveDynamic) =
            ResolvePhraseVelocities(track, note.Velocity, rampVelocity, result);
        var midiNumber = notation.ResolvedMidiNumber;
        PlaybackShapeResult? lastShaped = null;

        foreach (var layer in GetPlaybackLayers(track))
        {
            var shaped = PlaybackShaper.ShapeNote(
                resolvedNoteVelocity,
                resolvedRampVelocity,
                notation.Dynamic,
                effectiveDynamic,
                track.CurrentVelocity,
                notation.Articulation,
                layer.InstrumentName,
                notation.DurationBeats);

            ApplyPlaybackWarnings(result, shaped);
            lastShaped = shaped;

            var durationMs = result.TempoMap.BeatsToMilliseconds(globalBeat, shaped.DurationBeats);
            var velocity = ApplyTrackGain(shaped.Velocity, track.Gain);

            track.Notes.Add(new TimedNote(
                midiNumber,
                globalBeat,
                shaped.DurationBeats,
                durationMs,
                velocity,
                layer.Channel));
        }

        if (lastShaped is not null)
        {
            notation = notation with
            {
                ShapedVelocity = lastShaped.Value.Velocity,
                ShapedDurationBeats = lastShaped.Value.DurationBeats
            };
        }

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

    private static void EmitChord(TrackBuilder track, ChordNode chord, GlobalBeatClock clock, InterpretedProgram result)
    {
        var globalBeat = clock.ToGlobalBeat(track.CurrentBeat, track.GlobalOffset);
        MaybeApplySyncCorrection(track, globalBeat, clock, result);

        var (voicedNotes, voicedAdjusted) = ChordVoicing.Apply(chord.ToMidiNumbers());
        if (voicedAdjusted)
            AddWarning(result, "Chord voicing adjustment applied");

        var (advancedNotes, advancedAdjusted) = AdvancedChordVoicing.Apply(voicedNotes, chord.Voicing);
        if (advancedAdjusted)
            AddWarning(result, "Advanced chord voicing applied");

        var (spacedNotes, spacedAdjusted) = HarmonicSpacing.Apply(advancedNotes);
        if (spacedAdjusted)
            AddWarning(result, "Harmonic spacing adjustment applied");

        var durationMs = result.TempoMap.BeatsToMilliseconds(globalBeat, chord.DurationBeats);
        var durationBeats = BeatMath.RoundBeat(chord.DurationBeats);

        foreach (var layer in GetPlaybackLayers(track))
        {
            var (resolvedChordVelocity, _, effectiveDynamic) =
                ResolvePhraseVelocities(track, chord.Velocity, null, result);

            var shaped = PlaybackShaper.ShapeChordVelocity(
                resolvedChordVelocity,
                effectiveDynamic,
                track.CurrentVelocity,
                layer.InstrumentName);

            ApplyPlaybackWarnings(result, shaped);

            var (balancedVelocities, balanced) = ChordBalancer.Apply(spacedNotes, shaped.Velocity);
            if (balanced)
                AddWarning(result, "Chord balance applied");

            for (var i = 0; i < spacedNotes.Length; i++)
            {
                track.Notes.Add(new TimedNote(
                    spacedNotes[i],
                    globalBeat,
                    durationBeats,
                    durationMs,
                    ApplyTrackGain(balancedVelocities[i], track.Gain),
                    layer.Channel));
            }
        }

        AdvanceTiming(track, chord.DurationBeats);
    }

    private static (int? NoteVelocity, int? RampVelocity, DynamicLevel? EffectiveDynamic) ResolvePhraseVelocities(
        TrackBuilder track,
        int? noteVelocity,
        int? rampVelocity,
        InterpretedProgram result)
    {
        var effectiveDynamic = track.ActivePhrase?.Dynamic ?? track.CurrentDynamic;

        if (track.ActivePhrase is null)
            return (noteVelocity, rampVelocity, effectiveDynamic);

        var baseVelocity = noteVelocity
            ?? rampVelocity
            ?? effectiveDynamic?.ToVelocity()
            ?? track.CurrentVelocity;

        var (phraseVelocity, shaped) = PhraseShaper.Apply(baseVelocity, track.ActivePhrase);
        if (shaped)
            AddWarning(result, "Phrase shaping applied");

        return (phraseVelocity, null, effectiveDynamic);
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

    private static int ApplyTrackGain(int velocity, double gain) =>
        Math.Clamp((int)Math.Round(velocity * gain), 1, 127);

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
