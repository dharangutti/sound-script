using SoundScript.Core;
using SoundScript.Core.Performance;

namespace SoundScript.Midi;

internal static class ExpressivePerformance
{
    internal static void AssignChannels(InterpretedProgram program)
    {
        var next = 0;
        foreach (var track in program.Tracks)
        {
            var channels = track.Notes.Select(n => n.Channel).Concat(track.ProgramChanges.Select(p => p.Channel)).Distinct().ToArray();
            var mapping = new Dictionary<byte, byte>();
            foreach (var old in channels)
            {
                if (next == 9) next++;
                if (next > 15) throw new InvalidOperationException("Expressive performance supports at most 15 independent instrument layers (MIDI channel 10 is reserved for percussion).");
                var channel = (byte)next++;
                mapping[old] = channel;
            }
            var initialPrograms = track.ProgramChanges.Where(p => p.Beat == 0).Select(p => p.Channel).ToHashSet();
            for (var i = 0; i < track.Notes.Count; i++)
                track.Notes[i] = track.Notes[i] with { Channel = mapping[track.Notes[i].Channel] };
            for (var i = 0; i < track.ProgramChanges.Count; i++)
                track.ProgramChanges[i] = track.ProgramChanges[i] with { Channel = mapping[track.ProgramChanges[i].Channel] };
            foreach (var old in channels.Where(c => !initialPrograms.Contains(c)))
                track.ProgramChanges.Add(new ProgramChange(0, 0, mapping[old]));
        }
    }

    internal static void Apply(InterpretedTrack track, TempoAutomationMap tempo)
    {
        foreach (var voice in track.Notes.Select((note, index) => (note, index))
                     .Where(x => x.note.PerformanceIntent is not null)
                     .GroupBy(x => (x.note.Channel, ChordPitch: x.note.PerformanceIntent!.IsChord ? x.note.MidiNumber : -1)))
        {
            var items = voice.ToArray();
            var events = items.Select(x =>
            {
                var n = x.note;
                var intent = n.PerformanceIntent!;
                return new PerformanceEvent(n.MidiNumber, Seconds(0, n.StartBeat), Seconds(n.StartBeat, n.DurationBeats),
                    Seconds(0, intent.StartBeat), Seconds(intent.StartBeat, intent.DurationBeats), n.Velocity / 127.0, intent);
            }).ToArray();
            var shapes = PerformancePlanner.Plan(events);
            for (var i = 0; i < items.Length; i++)
            {
                var note = items[i].note;
                var shape = shapes[i];
                var startBeat = BeatsForSeconds(0, shape.Start);
                // Invert the same integrated tempo map, including ramps, without a constant-BPM approximation.
                track.Notes[items[i].index] = note with
                {
                    StartBeat = Math.Round(startBeat, 9),
                    DurationBeats = Math.Round(BeatsForSeconds(startBeat, shape.Duration), 9),
                    DurationMs = shape.Duration * 1000,
                    Velocity = Math.Clamp((int)Math.Round(shape.Velocity * 127), 1, 127),
                    Performance = shape
                };
            }
        }
        double Seconds(double beat, double duration) => tempo.BeatsToMilliseconds(beat, duration) / 1000;
        double BeatsForSeconds(double startBeat, double seconds)
        {
            if (seconds <= 0) return 0;
            var low = 0.0;
            var high = 1.0;
            while (Seconds(startBeat, high) < seconds) high *= 2;
            for (var iteration = 0; iteration < 48; iteration++)
            {
                var mid = (low + high) / 2;
                if (Seconds(startBeat, mid) < seconds) low = mid; else high = mid;
            }
            return (low + high) / 2;
        }
    }
}
