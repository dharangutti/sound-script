using SoundScript.Core;

namespace SoundScript.Midi;

public static class Interpreter
{
    public static IReadOnlyList<TimedNote> Interpret(MelodyProgram program)
    {
        var timedNotes = new List<TimedNote>();
        var currentBeat = 0.0;

        foreach (var note in program.Notes)
        {
            var durationMs = BeatsToMilliseconds(note.DurationBeats, program.Bpm);
            timedNotes.Add(new TimedNote(
                note.ToMidiNumber(),
                currentBeat,
                note.DurationBeats,
                durationMs));

            currentBeat += note.DurationBeats;
        }

        return timedNotes;
    }

    public static double BeatsToMilliseconds(double beats, int bpm) =>
        (60_000.0 / bpm) * beats;
}
