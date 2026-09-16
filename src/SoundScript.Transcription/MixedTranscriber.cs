namespace SoundScript.Transcription;

public sealed record MusicalRoleEvidence(string Role, bool Selected, int DetectedNotes,
    double MeanRankAgreement, string Method);
public sealed record MixedEvidence(IReadOnlyList<MusicalRoleEvidence> Roles, double UnsupportedFrameFraction,
    string SeparationKind = "Symbolic musical-role estimates only; no isolated audio stems");

/// <summary>Assigns supported simultaneous notes to conservative register/rank roles.
/// Acoustic extraction stays in the shared polyphonic analyzer; no stem-isolation claim.</summary>
public static class MixedTranscriber
{
    public static readonly IReadOnlyList<string> AvailableRoles = Array.AsReadOnly(new[] { "melody", "harmony", "bass" });

    public static IReadOnlyList<string> ValidateRoles(IReadOnlyList<string>? roles)
    {
        var selected = roles ?? AvailableRoles;
        if (selected.Count == 0 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Count || selected.Any(r => !AvailableRoles.Contains(r)))
            throw new ArgumentException("Select one or more distinct roles: melody, harmony, bass.", nameof(roles));
        return selected;
    }

    public static TranscriptionResult Interpret(PolyphonicObservations observations, TranscriptionOptions options)
    {
        var selected = ValidateRoles(options.Roles);
        var result = PolyphonicTranscriber.Interpret(observations, options);
        var notes = result.Score.Tracks.SelectMany(t => t.Notes).OrderBy(n => n.StartSeconds).ThenBy(n => n.MidiPitch).ToArray();
        var top = new int[notes.Length]; var bottom = new int[notes.Length]; var counts = new int[notes.Length];
        foreach (var frame in observations.Frames)
        {
            var active = Enumerable.Range(0, notes.Length).Where(i => notes[i].StartSeconds <= frame.Seconds && notes[i].StartSeconds + notes[i].DurationSeconds > frame.Seconds + 1e-8).ToArray();
            foreach (int i in active)
            {
                counts[i]++;
                if (active.All(j => i == j || notes[j].MidiPitch <= notes[i].MidiPitch - 3)) top[i]++;
                if (active.All(j => i == j || notes[j].MidiPitch > notes[i].MidiPitch)) bottom[i]++;
            }
        }
        double Fraction(int count, int total) => total == 0 ? 0 : count / (double)total;
        var assigned = notes.Select((note, i) => {
            double low = Fraction(bottom[i], counts[i]), high = Fraction(top[i], counts[i]);
            string role = note.MidiPitch <= 55 && low >= .8 ? "bass" : note.MidiPitch >= 60 && high >= .8 ? "melody" : "harmony";
            return (Note: note, Role: role, Agreement: role == "bass" ? low : role == "melody" ? high : 1 - Math.Max(low, high));
        }).ToArray();
        var tracks = new List<MusicalTrack>(); var evidence = new List<MusicalRoleEvidence>();
        foreach (string role in AvailableRoles)
        {
            var roleNotes = assigned.Where(n => n.Role == role).OrderBy(n => n.Note.StartBeat).ThenBy(n => n.Note.MidiPitch).ToArray();
            evidence.Add(new(role, selected.Contains(role), roleNotes.Length, roleNotes.Select(n => n.Agreement).DefaultIfEmpty().Average(),
                role == "bass" ? "Pitch <= G3 and strictly lowest supported note in >=80% of its observed frames; not instrument recognition"
                : role == "melody" ? "Pitch >= C4 and at least three semitones above others in >=80% of its observed frames; not verified lead identity"
                : "Remaining simultaneous notes; rank ambiguity is retained as harmony, not a named chord or stem"));
            if (!selected.Contains(role)) continue;
            var voices = new List<List<MusicalNote>>();
            foreach (var item in roleNotes)
            {
                var voice = voices.FirstOrDefault(v => v[^1].StartBeat + v[^1].DurationBeats <= item.Note.StartBeat + 1e-8);
                if (voice == null) { voice = []; voices.Add(voice); }
                voice.Add(item.Note);
            }
            double end = Math.Round(observations.DurationSeconds * result.Score.TempoMap[0].Bpm / 60, 6);
            foreach (var (voice, index) in voices.Select((v, i) => (v, i)))
            {
                var rests = new List<MusicalRest>(); double cursor = 0;
                foreach (var note in voice) { if (note.StartBeat > cursor + 1e-8) rests.Add(new(cursor, note.StartBeat - cursor)); cursor = note.StartBeat + note.DurationBeats; }
                if (end > cursor + 1e-8) rests.Add(new(cursor, end - cursor));
                tracks.Add(new($"{role}{index + 1}", role, options.Instrument, voice, rests));
            }
        }
        var diagnostics = result.Diagnostics.Concat(new[] {
            new AnalysisDiagnostic("symbolic-role-separation", "Roles are register/rank hypotheses over supported pitches, not isolated audio stems. A high accompaniment may be labelled melody; a low chord tone may be labelled bass."),
            new AnalysisDiagnostic("unsupported-mixed-material", "Percussion, vocals/lyrics and diffuse mixture sections are not reconstructed by this mode. Rejected polyphonic frames remain explicit in the report.")
        }).ToArray();
        return result with {
            Score = result.Score with { Tracks = tracks }, Diagnostics = diagnostics,
            Mixed = new(evidence, result.Polyphony!.AmbiguousFrameFraction)
        };
    }
}
