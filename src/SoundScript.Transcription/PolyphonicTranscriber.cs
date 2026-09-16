namespace SoundScript.Transcription;

public static class PolyphonicTranscriber
{
    public static TranscriptionResult Interpret(PolyphonicObservations observations, TranscriptionOptions options)
    {
        if (options.Tempo is < 20 or > 300 || options.Instrument is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(options));
        var frames = observations.Frames;
        if (!double.IsFinite(observations.DurationSeconds) || observations.DurationSeconds < 0 ||
            frames.Any(f => !double.IsFinite(f.Seconds) || f.Seconds < 0 || f.Seconds >= observations.DurationSeconds || !double.IsFinite(f.Rms) || f.Rms < 0) ||
            frames.Zip(frames.Skip(1), (a,b) => b.Seconds <= a.Seconds).Any(x => x))
            throw new InvalidDataException("Polyphonic frames must have finite, increasing times within the recording and finite nonnegative RMS.");
        var segments = new List<(int Pitch, double Start, double End, double Evidence, double Rms)>();
        for (int pitch = PolyphonicAnalyzer.MinimumPitch; pitch <= PolyphonicAnalyzer.MaximumPitch; pitch++)
        {
            int begin = -1;
            var energies = frames.Select(f => f.Pitches.FirstOrDefault(p => p.MidiPitch == pitch)?.FundamentalShare * f.Rms * f.Rms ?? 0).ToArray();
            double peakEnergy = energies.DefaultIfEmpty().Max();
            for (int i = 0; i <= frames.Count; i++)
            {
                bool present = i < frames.Count && energies[i] > peakEnergy * .025 && energies[i] > 0;
                bool attack = present && begin >= 0 && i - begin >= 8 && i >= 3 &&
                    energies[i] > energies[i - 3] * 2.5 && energies[i] > peakEnergy * .15;
                if (begin >= 0 && (!present || attack))
                {
                    double end = i < frames.Count ? frames[i].Seconds : observations.DurationSeconds;
                    if (end - frames[begin].Seconds >= .1 - 1e-8)
                    {
                        var section = frames.Skip(begin).Take(i - begin).ToArray();
                        segments.Add((pitch, frames[begin].Seconds, end,
                            section.Average(f => f.Pitches.First(p => p.MidiPitch == pitch).ResidualShare), section.Average(f => f.Rms)));
                    }
                    begin = -1;
                }
                if (present && begin < 0) begin = i;
            }
        }
        var onsets = segments.Select(n => n.Start).Distinct().Order().ToArray();
        int tempo = options.Tempo ?? MonophonicTranscriber.EstimateTempo(onsets);
        double factor = tempo / 60.0;
        var notes = segments.Select(n => {
            double start = n.Start * factor, end = n.End * factor;
            if (options.Quantize)
            {
                double a = MonophonicTranscriber.Quantize(start), b = MonophonicTranscriber.Quantize(end);
                if (b > a) { start = a; end = b; }
            }
            start = Math.Round(start, 6); end = Math.Round(end, 6);
            return new MusicalNote(n.Pitch, n.Start, n.End - n.Start, start, Math.Round(end - start, 6),
                Math.Clamp((int)Math.Round(100 * Math.Sqrt(n.Rms)), 35, 100),
                new(n.Evidence, "Mean fundamental spectral energy after conservative lower-harmonic accounting / total energy; not a correctness probability"));
        }).OrderBy(n => n.StartBeat).ThenBy(n => n.MidiPitch).ToArray();
        // Interval partitioning preserves arbitrary overlap without guessing named chords,
        // adding syntax, or flattening notes. Every voice is a normal sequential track.
        var voices = new List<List<MusicalNote>>();
        foreach (var note in notes)
        {
            var voice = voices.FirstOrDefault(v => v[^1].StartBeat + v[^1].DurationBeats <= note.StartBeat + 1e-8);
            if (voice == null) { voice = []; voices.Add(voice); }
            voice.Add(note);
        }
        if (voices.Count == 0) voices.Add([]);
        double totalBeats = Math.Round(observations.DurationSeconds * factor, 6);
        var tracks = voices.Select((voice, i) => {
            var rests = new List<MusicalRest>(); double cursor = 0;
            foreach (var note in voice) { if (note.StartBeat > cursor + 1e-8) rests.Add(new(cursor, note.StartBeat - cursor)); cursor = note.StartBeat + note.DurationBeats; }
            if (totalBeats > cursor) rests.Add(new(cursor, totalBeats - cursor));
            return new MusicalTrack($"pianovoice{i + 1}", "polyphonic-voice", options.Instrument, voice, rests);
        }).ToArray();
        var boundaries = notes.SelectMany(n => new[] { n.StartSeconds, n.StartSeconds + n.DurationSeconds }).Distinct().Order().ToArray();
        var groups = new List<SimultaneousPitchGroup>();
        for (int i = 1; i < boundaries.Length; i++)
        {
            double start = boundaries[i - 1], end = boundaries[i], middle = (start + end) / 2;
            var pitches = notes.Where(n => n.StartSeconds <= middle && n.StartSeconds + n.DurationSeconds > middle).Select(n => n.MidiPitch).Distinct().Order().ToArray();
            if (pitches.Length < 2) continue;
            if (groups.Count > 0 && Math.Abs(groups[^1].StartSeconds + groups[^1].DurationSeconds - start) < 1e-8 && groups[^1].Pitches.SequenceEqual(pitches))
                groups[^1] = groups[^1] with { DurationSeconds = end - groups[^1].StartSeconds };
            else groups.Add(new(start, end - start, pitches));
        }
        double threshold = Math.Max(.003, frames.Select(f => f.Rms).DefaultIfEmpty().Max() * .035);
        var active = frames.Where(f => f.Rms >= threshold).ToArray();
        int CountAt(double t) => notes.Count(n => n.StartSeconds <= t && n.StartSeconds + n.DurationSeconds > t + 1e-8);
        double Fraction(Func<PolyphonicFrame, bool> predicate) => active.Length == 0 ? 0 : active.Count(predicate) / (double)active.Length;
        double fit = notes.Length == 0 ? 0 : notes.SelectMany(n => new[] { n.StartSeconds, n.StartSeconds + n.DurationSeconds })
            .Average(t => Math.Max(0, 1 - Math.Abs(t * factor * 4 - Math.Round(t * factor * 4)) / .5));
        var diagnostics = observations.Diagnostics.ToList();
        if (options.Tempo == null) diagnostics.Add(new("uncertain-tempo", "Tempo uses grouped onset-grid evidence; half/double time and rubato remain ambiguous."));
        if (notes.Length == 0) diagnostics.Add(new("no-notes", "No supported pitch lasted at least 100 ms."));
        var evidence = new PolyphonicEvidence(observations, notes.Length,
            frames.Select(f => CountAt(f.Seconds)).DefaultIfEmpty().Max(), groups.Count,
            active.Select(f => (double)CountAt(f.Seconds)).DefaultIfEmpty().Average(), Fraction(f => CountAt(f.Seconds) >= 2),
            Fraction(f => CountAt(f.Seconds) > 0), notes.Select(n => n.PitchEvidence.Confidence).DefaultIfEmpty().Average(),
            Fraction(f => f.Rejection != null), Fraction(f => f.OctaveAmbiguous), groups);
        var score = new MusicalScore([new(0, tempo, new(options.Tempo.HasValue ? 1 : fit, options.Tempo.HasValue ? "User supplied" : "Onset grid fit only"))], null, null, tracks, [], observations.DurationSeconds);
        // Do not put simultaneous candidates into the one-frequency observation type.
        return new(score, new([], observations.DurationSeconds, diagnostics), evidence.MeanFundamentalShare, fit, diagnostics) { Polyphony = evidence };
    }
}
