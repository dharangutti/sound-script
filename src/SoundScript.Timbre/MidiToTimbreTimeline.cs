using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript.Compose;

namespace SoundScript.Timbre;

/// <summary>One note-aligned timbre segment on the offline render timeline.</summary>
public sealed record TimbreSegment(
    double StartMs,
    double DurationMs,
    double PitchHz,
    int Velocity,
    string Phoneme,
    TimbreProfile Profile);

/// <summary>Frame-rate timeline with cycle-accurate frame plans (V4.1).</summary>
public sealed class TimbreTimeline
{
    public required int SampleRate { get; init; }
    public required double FrameMs { get; init; }
    public required double TotalDurationMs { get; init; }
    public required IReadOnlyList<TimbreSegment> Segments { get; init; }
    public required IReadOnlyList<TimbreFramePlan> Frames { get; init; }

    public int FrameCount => Frames.Count;
    public int SampleCount => (int)Math.Ceiling(TotalDurationMs * SampleRate / 1000.0);
}

/// <summary>
/// Reads a MIDI file, extracts note events, aligns phonemes to note timing,
/// and produces a deterministic frame timeline with per-frame cycle counts.
/// </summary>
public static class MidiToTimbreTimeline
{
    public const int DefaultSampleRate = 44100;
    public const double DefaultFrameMs = 8.0;
    private const int TicksPerQuarterNote = 480;

    /// <summary>Builds a timbre timeline from a MIDI file path.</summary>
    public static TimbreTimeline Build(
        string midiPath,
        IReadOnlyDictionary<string, TimbreProfileOverrides>? cssOverrides = null,
        IReadOnlyList<string>? phonemes = null,
        int sampleRate = DefaultSampleRate,
        double frameMs = DefaultFrameMs,
        string? preferredTrackName = PhraseAssembler.TrackName)
    {
        var midiFile = MidiFile.Read(midiPath);
        return Build(midiFile, cssOverrides, phonemes, sampleRate, frameMs, preferredTrackName);
    }

    /// <summary>Builds a timbre timeline from an in-memory MIDI file.</summary>
    public static TimbreTimeline Build(
        MidiFile midiFile,
        IReadOnlyDictionary<string, TimbreProfileOverrides>? cssOverrides = null,
        IReadOnlyList<string>? phonemes = null,
        int sampleRate = DefaultSampleRate,
        double frameMs = DefaultFrameMs,
        string? preferredTrackName = PhraseAssembler.TrackName)
    {
        var tempoMicroseconds = GetInitialTempo(midiFile);
        var notes = ExtractNotes(midiFile, tempoMicroseconds, preferredTrackName);
        var segments = AlignPhonemes(notes, cssOverrides, phonemes);
        var totalDurationMs = segments.Count == 0
            ? 0
            : segments.Max(segment => segment.StartMs + segment.DurationMs) + frameMs;

        var frames = BuildFramePlans(segments, frameMs, totalDurationMs);

        return new TimbreTimeline
        {
            SampleRate = sampleRate,
            FrameMs = frameMs,
            TotalDurationMs = totalDurationMs,
            Segments = segments,
            Frames = frames
        };
    }

    /// <summary>Builds per-frame cycle plans from aligned segments.</summary>
    public static IReadOnlyList<TimbreFramePlan> BuildFramePlans(
        IReadOnlyList<TimbreSegment> segments,
        double frameMs,
        double totalDurationMs)
    {
        var frameCount = totalDurationMs <= 0 ? 0 : (int)Math.Ceiling(totalDurationMs / frameMs);
        var frames = new List<TimbreFramePlan>(frameCount);

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var startMs = frameIndex * frameMs;
            var segment = FindActiveSegment(segments, startMs);
            if (segment is null)
                continue;

            var profile = InterpolateProfile(segment, startMs);
            var cycleLengthMs = CycleGenerator.CycleLengthMs(segment.PitchHz);
            var cycleCount = CyclePlanner.CycleCountForFrame(segment.PitchHz, frameMs);

            frames.Add(new TimbreFramePlan(
                frameIndex,
                startMs,
                segment.StartMs,
                segment.DurationMs,
                segment.PitchHz,
                segment.Velocity,
                segment.Phoneme,
                profile,
                cycleCount,
                cycleLengthMs));
        }

        return frames;
    }

    private static TimbreProfile InterpolateProfile(TimbreSegment segment, double timeMs)
    {
        var position = (timeMs - segment.StartMs) / Math.Max(segment.DurationMs, 1.0);
        var smooth = Math.Clamp(segment.Profile.Smoothness, 0, 1);
        var blend = smooth > 0 ? Math.Pow(position, 1.0 + smooth * 3.0) : position;
        var openness = segment.Profile.Openness * (0.6 + 0.4 * blend);

        // Phoneme-specific smoothing hint: onsets adapt faster than held mid-note frames,
        // so consonant attacks stay crisp while sustained vowels blend smoothly (V4.1.1).
        var smoothingHint = segment.Profile.FrameSmoothing * (0.5 + 0.5 * blend);

        return segment.Profile.With(
            formant1Hz: segment.Profile.Formant1Hz * (0.85 + 0.3 * openness),
            formant2Hz: segment.Profile.Formant2Hz * (0.9 + 0.2 * openness),
            formant3Hz: segment.Profile.Formant3Hz * (0.92 + 0.15 * openness),
            frameSmoothing: smoothingHint);
    }

    private static TimbreSegment? FindActiveSegment(IReadOnlyList<TimbreSegment> segments, double timeMs)
    {
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var segment = segments[i];
            if (timeMs >= segment.StartMs && timeMs < segment.StartMs + segment.DurationMs)
                return segment;
        }

        return null;
    }

    private static List<NoteEvent> ExtractNotes(
        MidiFile midiFile,
        int tempoMicroseconds,
        string? preferredTrackName)
    {
        var events = new List<NoteEvent>();
        var trackIndex = 0;

        foreach (var chunk in midiFile.Chunks.OfType<TrackChunk>())
        {
            var trackName = GetTrackName(chunk) ?? $"track-{trackIndex}";
            trackIndex++;

            if (preferredTrackName is not null
                && !string.Equals(trackName, preferredTrackName, StringComparison.Ordinal)
                && events.Count > 0)
                continue;

            using var notesManager = chunk.ManageNotes();
            foreach (var note in notesManager.Objects.OrderBy(n => n.Time))
            {
                var startMs = TicksToMilliseconds(note.Time, tempoMicroseconds);
                var durationMs = TicksToMilliseconds(note.Length, tempoMicroseconds);
                var durationBeats = note.Length / (double)TicksPerQuarterNote;
                events.Add(new NoteEvent(
                    trackName,
                    note.NoteNumber,
                    note.Velocity,
                    startMs,
                    durationMs,
                    durationBeats));
            }

            if (preferredTrackName is not null
                && string.Equals(trackName, preferredTrackName, StringComparison.Ordinal)
                && events.Count > 0)
                break;
        }

        if (events.Count == 0)
        {
            foreach (var chunk in midiFile.Chunks.OfType<TrackChunk>())
            {
                using var notesManager = chunk.ManageNotes();
                foreach (var note in notesManager.Objects.OrderBy(n => n.Time))
                {
                    var startMs = TicksToMilliseconds(note.Time, tempoMicroseconds);
                    var durationMs = TicksToMilliseconds(note.Length, tempoMicroseconds);
                    var durationBeats = note.Length / (double)TicksPerQuarterNote;
                    events.Add(new NoteEvent(
                        "all",
                        note.NoteNumber,
                        note.Velocity,
                        startMs,
                        durationMs,
                        durationBeats));
                }
            }
        }

        return events.OrderBy(note => note.StartMs).ThenBy(note => note.MidiNumber).ToList();
    }

    private static List<TimbreSegment> AlignPhonemes(
        IReadOnlyList<NoteEvent> notes,
        IReadOnlyDictionary<string, TimbreProfileOverrides>? cssOverrides,
        IReadOnlyList<string>? phonemes)
    {
        var segments = new List<TimbreSegment>(notes.Count);
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var phoneme = phonemes is not null && i < phonemes.Count
                ? phonemes[i]
                : PhonemeTimbreMapper.GuessPhoneme(note.MidiNumber, note.DurationBeats, note.Velocity);

            segments.Add(new TimbreSegment(
                note.StartMs,
                note.DurationMs,
                MidiToFrequency(note.MidiNumber),
                note.Velocity,
                phoneme,
                PhonemeTimbreMapper.Map(phoneme, cssOverrides)));
        }

        return segments;
    }

    private static int GetInitialTempo(MidiFile midiFile)
    {
        foreach (var chunk in midiFile.Chunks.OfType<TrackChunk>())
        {
            foreach (var tempoEvent in chunk.Events.OfType<SetTempoEvent>())
                return (int)tempoEvent.MicrosecondsPerQuarterNote;
        }

        return 625_000; // 96 BPM — PhonemeComposer default
    }

    private static string? GetTrackName(TrackChunk chunk)
    {
        foreach (var sequenceEvent in chunk.Events.OfType<SequenceTrackNameEvent>())
            return sequenceEvent.Text;
        return null;
    }

    private static double TicksToMilliseconds(long ticks, int microsecondsPerQuarterNote) =>
        ticks * microsecondsPerQuarterNote / (double)TicksPerQuarterNote / 1000.0;

    private static double MidiToFrequency(int midiNumber) =>
        440.0 * Math.Pow(2.0, (midiNumber - 69) / 12.0);

    private sealed record NoteEvent(
        string TrackName,
        int MidiNumber,
        int Velocity,
        double StartMs,
        double DurationMs,
        double DurationBeats);
}
