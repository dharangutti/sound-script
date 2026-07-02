using SoundScript.Core;

namespace SoundScript.Midi;

public static partial class MidiGenerator
{
    public static void Write(MelodyProgram program, IReadOnlyList<TimedNote> timedNotes, Stream output)
    {
        var midiFile = CreateMidiFile(program, timedNotes);
        midiFile.Write(output);
    }
}
