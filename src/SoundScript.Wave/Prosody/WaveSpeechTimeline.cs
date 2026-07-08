// UNDER DEVELOPMENT — v3
using SoundScript.Core;
using SoundScript.Core.Ast;

namespace SoundScript.Wave.Prosody;

/// <summary>
/// One spoken phrase for the playback-only speech overlay: absolute onset and
/// duration in milliseconds (tempo-map accurate) plus a neutral MIDI pitch.
///
/// This mirrors the JSON shape the playground's <c>voice-speech.js</c> bridge
/// (<c>SoundScriptVoice.speak(words)</c>) consumes. System.Text.Json's Web
/// defaults (used by Blazor JSInterop) serialize these PascalCase properties as
/// the camelCase keys the bridge reads: <c>text</c>, <c>startMs</c>,
/// <c>durationMs</c>, <c>midi</c>.
///
/// Deliberately a local record rather than a reuse of
/// SoundScript.Voice.VocalSpeechWord: SoundScript.Wave must not take a project
/// reference on SoundScript.Voice.
/// </summary>
public sealed record WaveSpeechWord(string Text, double StartMs, double DurationMs, int Midi);

/// <summary>
/// Walks the same unmodified AST as <see cref="Adapter.AstToNoteEventAdapter"/>
/// and produces one <see cref="WaveSpeechWord"/> per <c>speak "..."</c>
/// directive, timed at the beat cursor where that directive's prosody tones are
/// emitted into the WAV. This is a playback-only overlay: it does not affect the
/// rendered audio bytes in any way (the prosody tones stay in the WAV), so it
/// leaves WAV determinism untouched.
///
/// Timing semantics match the adapter exactly:
/// <list type="bullet">
/// <item>A top-level <c>speak</c> advances (and reads) the <c>default</c>
/// track's cursor — which is independent of any <c>track { }</c> / <c>melody { }</c>
/// block's cursor.</item>
/// <item>A <c>speak</c> inside a track body advances that track's cursor.</item>
/// <item>Beats convert to milliseconds through the same shared
/// <see cref="TempoAutomationMap"/> the adapter uses.</item>
/// </list>
/// Fully deterministic: no wall-clock, no unseeded Random. The neutral pitch is
/// C4 (MIDI 60), which <c>voice-speech.js</c>'s <c>pitchForMidi</c> maps to
/// natural speaking pitch.
/// </summary>
public static class WaveSpeechTimeline
{
    // C4 — voice-speech.js pitchForMidi(60) == natural (1.0) speaking pitch.
    private const int NeutralMidi = 60;

    public static IReadOnlyList<WaveSpeechWord> Build(ProgramNode program)
    {
        var context = new BuildContext();
        var tracks = new Dictionary<string, TrackCursor>(StringComparer.OrdinalIgnoreCase);
        var trackOrder = new List<string>();
        TrackCursor? defaultTrack = null;

        TrackCursor GetDefaultTrack()
        {
            defaultTrack ??= GetOrCreateTrack(tracks, trackOrder, "default");
            return defaultTrack;
        }

        var words = new List<WaveSpeechWord>();
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
                    context.PatternNames.Add(pattern.Name);
                    break;
                case TrackNode track:
                    ExecuteStatements(GetOrCreateTrack(tracks, trackOrder, track.Name), track.Body, context, words);
                    break;
                case MelodyNode melody:
                    ExecuteStatements(GetOrCreateTrack(tracks, trackOrder, "melody"), melody.Body, context, words);
                    break;
                case PlayNode play:
                    ExecutePlay(GetDefaultTrack(), play, context, words);
                    break;
                case LoopNode loop:
                    ExecuteLoop(GetDefaultTrack(), loop, context, words);
                    break;
                case RestNode rest:
                    AdvanceBeat(GetDefaultTrack(), rest.Rest.DurationBeats);
                    break;
                case NoteNode note:
                    AdvanceBeat(GetDefaultTrack(), note.DurationBeats);
                    break;
                case ChordNode chord:
                    AdvanceBeat(GetDefaultTrack(), chord.DurationBeats);
                    break;
                case SpeakNode speak:
                    EmitSpeech(GetDefaultTrack(), speak, context, words);
                    break;
            }
        }

        return words;
    }

    private static void ExecuteStatements(
        TrackCursor track, IReadOnlyList<AstNode> body, BuildContext context, List<WaveSpeechWord> words)
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
                case RestNode rest:
                    AdvanceBeat(track, rest.Rest.DurationBeats);
                    break;
                case NoteNode note:
                    AdvanceBeat(track, note.DurationBeats);
                    break;
                case ChordNode chord:
                    AdvanceBeat(track, chord.DurationBeats);
                    break;
                case LoopNode loop:
                    ExecuteLoop(track, loop, context, words);
                    break;
                case PlayNode play:
                    ExecutePlay(track, play, context, words);
                    break;
                case SpeakNode speak:
                    EmitSpeech(track, speak, context, words);
                    break;

                // velocity/dynamic/humanize do not advance the beat cursor and
                // have no bearing on speech timing — intentionally skipped here.
            }
        }
    }

    private static void ExecutePlay(TrackCursor track, PlayNode play, BuildContext context, List<WaveSpeechWord> words)
    {
        if (context.Blocks.TryGetValue(play.SequenceName, out var blockBody))
        {
            ExecuteStatements(track, blockBody, context, words);
            return;
        }

        if (context.Sequences.TryGetValue(play.SequenceName, out var sequenceBody))
        {
            ExecuteStatements(track, sequenceBody, context, words);
            return;
        }

        // Unknown / pattern plays are handled (and thrown) by the adapter during
        // the WAV render that always precedes this pass; a program that reaches
        // here is already known-valid, so we simply ignore unresolved plays
        // rather than duplicating the adapter's exception policy.
    }

    private static void ExecuteLoop(TrackCursor track, LoopNode loop, BuildContext context, List<WaveSpeechWord> words)
    {
        for (var i = 0; i < loop.Count; i++)
            ExecuteStatements(track, loop.Body, context, words);
    }

    /// <summary>
    /// Emits one <see cref="WaveSpeechWord"/> for a <c>speak</c> directive and
    /// advances the track cursor by the total duration its prosody tones occupy,
    /// exactly as <see cref="Adapter.AstToNoteEventAdapter.EmitSpeech"/> does —
    /// so the overlay and the baked prosody tones stay phase-aligned.
    /// </summary>
    private static void EmitSpeech(TrackCursor track, SpeakNode speak, BuildContext context, List<WaveSpeechWord> words)
    {
        var startBeat = track.CurrentBeat;

        var totalBeats = 0.0;
        foreach (var tone in ProsodyToneGenerator.Generate(speak.Text, speak.Voice, speak.Seed))
            totalBeats += tone.DurationBeats;

        var startMs = context.TempoMap.BeatsToMilliseconds(0, startBeat);
        var durationMs = context.TempoMap.BeatsToMilliseconds(startBeat, totalBeats);

        words.Add(new WaveSpeechWord(speak.Text, startMs, durationMs, NeutralMidi));

        AdvanceBeat(track, totalBeats);
    }

    private static void AdvanceBeat(TrackCursor track, double beats) =>
        track.CurrentBeat += Math.Max(0.0, beats);

    private static double GetBeatsPerBar(BuildContext context)
    {
        var numerator = context.TimeSignatureNumerator ?? 4;
        var denominator = context.TimeSignatureDenominator ?? 4;
        return numerator * 4.0 / denominator;
    }

    private static void ScheduleTempoRamp(BuildContext context, double startBeat, TempoRampNode ramp)
    {
        var durationBeats = ramp.Bars * GetBeatsPerBar(context);
        context.TempoMap.AddRamp(startBeat, durationBeats, ramp.StartBpm, ramp.EndBpm);
    }

    private static TrackCursor GetOrCreateTrack(Dictionary<string, TrackCursor> tracks, List<string> order, string name)
    {
        if (tracks.TryGetValue(name, out var track))
            return track;

        track = new TrackCursor();
        tracks[name] = track;
        order.Add(name);
        return track;
    }

    private sealed class TrackCursor
    {
        public double CurrentBeat { get; set; }
    }

    private sealed class BuildContext
    {
        public TempoAutomationMap TempoMap { get; } = new();
        public Dictionary<string, List<AstNode>> Sequences { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<AstNode>> Blocks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PatternNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int? TimeSignatureNumerator { get; set; }
        public int? TimeSignatureDenominator { get; set; }
    }
}
