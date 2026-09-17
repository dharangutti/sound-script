using System.Text.Json;
using SoundScript.Core.Ast;
using SoundScript.Core.Notation;
using SoundScript.Parser;

namespace SoundScript.Transcription;

/// <summary>Writes a supported transcription score as a parsed SoundScript program or source string.</summary>
/// <remarks>The current writer emits a single tempo, pitched monophonic tracks, and percussion tracks.
/// Meter, key, sections, chords, lyrics, and expression metadata require an extended writer.</remarks>
public sealed class SoundScriptOutput : ITranscriptionOutput<ProgramNode>
{
    /// <summary>Converts a supported score to the SoundScript syntax tree.</summary>
    /// <param name="score">Score to write.</param>
    /// <returns>A program AST that can be rendered by the existing parser and renderers.</returns>
    /// <exception cref="NotSupportedException">The score contains metadata or events not supported by this writer.</exception>
    public ProgramNode Write(MusicalScore score)
    {
        if (score.TempoMap.Count != 1 || score.TempoMap[0].Beat != 0 || !double.IsFinite(score.TempoMap[0].Bpm)
            || score.TempoMap[0].Bpm is < 20 or > 300 || score.TempoMap[0].Bpm != Math.Round(score.TempoMap[0].Bpm))
            throw new NotSupportedException("Source writer currently requires one tempo at beat zero (20-300 BPM).");
        if (score.Meter != null || score.Key != null || score.Sections.Count != 0)
            throw new NotSupportedException("Meter, key and section emission are not yet supported.");
        var ast = new ProgramNode();
        ast.Statements.Add(new TempoNode { Bpm = (int)Math.Round(score.TempoMap[0].Bpm) });
        foreach (var track in score.Tracks)
        {
            if (track.Instrument is < 0 or > 127 || track.Rests.Any(r => !double.IsFinite(r.StartBeat) || !double.IsFinite(r.DurationBeats)
                || r.StartBeat < 0 || r.DurationBeats <= 0 || track.Notes.Any(n => r.StartBeat < n.StartBeat+n.DurationBeats-1e-7 && r.StartBeat+r.DurationBeats > n.StartBeat+1e-7)))
                throw new NotSupportedException("Invalid instrument or rest overlapping a note.");
            if (track.Chords?.Count > 0 || track.LegitimateText != null || track.Notes.Any(n => n.Expression?.Count > 0))
                throw new NotSupportedException("Chord, lyric and expression emission requires an extended output writer.");
            var node = new TrackNode { Name = track.Name };
            if (track.Percussion != null)
            {
                if (track.Notes.Count > 0) throw new NotSupportedException("Keep pitched notes and percussion in separate tracks.");
                double hitCursor = 0;
                foreach (var hit in track.Percussion.OrderBy(h => h.StartBeat))
                {
                    if (!Enum.IsDefined(hit.Sound) || !double.IsFinite(hit.StartBeat) || !double.IsFinite(hit.DurationBeats) ||
                        hit.StartBeat < hitCursor - 1e-7 || hit.DurationBeats <= 0 || hit.Velocity is < 1 or > 127 ||
                        track.Rests.Any(r => r.StartBeat < hit.StartBeat + hit.DurationBeats - 1e-7 && r.StartBeat + r.DurationBeats > hit.StartBeat + 1e-7))
                        throw new NotSupportedException("Invalid, overlapping percussion events or rests.");
                    AddRest(node, hit.StartBeat - hitCursor);
                    node.Body.Add(new HitNode { Sound = hit.Sound, DurationBeats = hit.DurationBeats, Velocity = hit.Velocity });
                    hitCursor = hit.StartBeat + hit.DurationBeats;
                }
                AddRest(node, track.Rests.Select(r => r.StartBeat + r.DurationBeats).DefaultIfEmpty(hitCursor).Max() - hitCursor);
                ast.Statements.Add(node);
                continue;
            }
            node.Body.Add(new InstrumentNode { ProgramNumber = track.Instrument });
            double cursor = 0;
            foreach (var note in track.Notes.OrderBy(n => n.StartBeat))
            {
                if (!double.IsFinite(note.StartBeat) || !double.IsFinite(note.DurationBeats) || note.StartBeat < cursor - 1e-7
                    || note.DurationBeats <= 0 || note.MidiPitch is < 12 or > 119 || note.Velocity is < 1 or > 127)
                    throw new NotSupportedException("Invalid or overlapping notes in a monophonic track.");
                AddRest(node, note.StartBeat - cursor);
                int pc = note.MidiPitch % 12;
                PitchClass[] pitches = [PitchClass.C,PitchClass.C,PitchClass.D,PitchClass.D,PitchClass.E,PitchClass.F,PitchClass.F,PitchClass.G,PitchClass.G,PitchClass.A,PitchClass.A,PitchClass.B];
                node.Body.Add(new NoteNode { Velocity = note.Velocity, Notation = new NotatedNote {
                    PitchClass = pitches[pc], Accidental = pc is 1 or 3 or 6 or 8 or 10 ? AccidentalType.Sharp : AccidentalType.None,
                    Octave = note.MidiPitch / 12 - 1, DurationBeats = Math.Round(note.DurationBeats,6),
                    StandardDuration = note.DurationBeats switch { 1 => NoteDuration.Quarter, 2 => NoteDuration.Half, .5 => NoteDuration.Eighth, 4 => NoteDuration.Whole, _ => null }
                }});
                cursor = note.StartBeat + note.DurationBeats;
            }
            double end = Math.Max(cursor, track.Rests.Select(r => r.StartBeat + r.DurationBeats).DefaultIfEmpty(cursor).Max());
            AddRest(node, end - cursor);
            ast.Statements.Add(node);
        }
        return ast;
    }
    private static void AddRest(TrackNode node, double beats)
    {
        if (beats > 1e-7) node.Body.Add(new RestNode { Rest = new NotatedRest { DurationBeats = Math.Round(beats,6) } });
    }
    /// <summary>Converts a supported score to canonical SoundScript source and reparses it for validation.</summary>
    /// <param name="score">Score to write.</param>
    /// <returns>Parseable SoundScript source.</returns>
    /// <exception cref="NotSupportedException">The score contains metadata or events not supported by this writer.</exception>
    public string Source(MusicalScore score)
    {
        string source = SsPrinter.Print(Write(score));
        _ = Parse(source); // The actual parser is the authority, including identifiers and durations.
        return source;
    }
    /// <summary>Parses SoundScript source into the canonical program syntax tree.</summary>
    /// <param name="source">Source text to parse.</param>
    /// <returns>The parsed program.</returns>
    /// <exception cref="InvalidOperationException">The source contains invalid SoundScript syntax.</exception>
    public static ProgramNode Parse(string source) => new SoundScript.Parser.Parser(new Tokenizer(source).Tokenize()).Parse();
}

/// <summary>Projects a transcription score onto a time-ordered list of pitched notes.</summary>
public sealed class TimelineOutput : ITranscriptionOutput<IReadOnlyList<MusicalNote>>
{
    /// <summary>Returns all pitched notes from all tracks ordered by source start time.</summary>
    /// <param name="score">Score to flatten.</param>
    /// <returns>An ordered, materialized note list.</returns>
    public IReadOnlyList<MusicalNote> Write(MusicalScore score) => score.Tracks.SelectMany(t => t.Notes).OrderBy(n => n.StartSeconds).ToArray();
}
/// <summary>Serializes transcription results as indented JSON.</summary>
public sealed class AnalysisJsonOutput
{
    /// <summary>JSON settings used by <see cref="Write(TranscriptionResult)"/>.</summary>
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    /// <summary>Serializes a transcription result, including available evidence and diagnostics.</summary>
    /// <param name="result">Result to serialize.</param>
    /// <returns>Indented JSON text.</returns>
    public string Write(TranscriptionResult result) => JsonSerializer.Serialize(result, Options);
}
