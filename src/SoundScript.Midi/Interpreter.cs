using SoundScript.Core;

namespace SoundScript.Midi;

public static class Interpreter
{
    public static IReadOnlyList<TimedNote> Interpret(MelodyProgram program, double defaultDurationBeats = 1.0)
    {
        var timedNotes = new List<TimedNote>();
        var currentBeat = 0.0;

        foreach (var note in program.Notes)
        {
            timedNotes.Add(new TimedNote(note.ToMidiNumber(), currentBeat, defaultDurationBeats));
            currentBeat += defaultDurationBeats;
        }

        return timedNotes;
    }

    public static double BeatsToMilliseconds(double beats, int bpm) =>
        beats * 60_000.0 / bpm;
}
