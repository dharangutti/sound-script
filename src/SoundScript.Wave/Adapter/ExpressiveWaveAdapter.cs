using SoundScript.Core.Performance;
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Adapter;

public static partial class AstToNoteEventAdapter
{
    private static void ApplyPerformance(List<NoteEvent> notes, ExecutionContext context)
    {
        foreach (var voice in notes.Select((note, index) => (note, index))
                     .Where(x => x.note.PerformanceIntent is not null)
                     .GroupBy(x => (x.note.PerformanceVoice, ChordPitch: x.note.PerformanceIntent!.IsChord ? x.note.FrequencyHz : -1)))
        {
            var items = voice.ToArray();
            var events = items.Select(x =>
            {
                var n = x.note;
                var intent = n.PerformanceIntent!;
                return new PerformanceEvent((int)Math.Round(69 + 12 * Math.Log2(n.FrequencyHz / 440)),
                    n.StartTimeSeconds, n.DurationSeconds, BeatsToSeconds(context, 0, intent.StartBeat),
                    BeatsToSeconds(context, intent.StartBeat, intent.DurationBeats), n.Velocity, intent);
            }).ToArray();
            var shapes = PerformancePlanner.Plan(events);
            for (var i = 0; i < items.Length; i++)
            {
                var note = items[i].note;
                var shape = shapes[i];
                var sustained = PerformancePlanner.IsSustained(note.PerformanceIntent!.Program);
                // Modest additive/triangle colors; these are synthetic timbres, not orchestral samples.
                var oscillator = note.PerformanceIntent.Program switch
                {
                    >= 40 and <= 54 => OscillatorType.Triangle,
                    >= 32 and <= 39 => OscillatorType.Triangle,
                    _ => OscillatorType.Sine
                };
                notes[items[i].index] = note with
                {
                    StartTimeSeconds = shape.Start,
                    DurationSeconds = shape.Duration,
                    Velocity = shape.Velocity,
                    Performance = shape,
                    Timbre = new TimbreParams(oscillator,
                        new Adsr(shape.Attack, sustained ? 0.08 : 0.12, sustained ? 0.86 : 0.55, shape.Release))
                };
            }
        }
    }
}
