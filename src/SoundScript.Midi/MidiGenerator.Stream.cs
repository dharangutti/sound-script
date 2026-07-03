using SoundScript.Core;

namespace SoundScript.Midi;

public static partial class MidiGenerator
{
    public static void Write(InterpretedProgram program, Stream output)
    {
        var midiFile = CreateMidiFile(program);
        midiFile.Write(output);
    }
}
