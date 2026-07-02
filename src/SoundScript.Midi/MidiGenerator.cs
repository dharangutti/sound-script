using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;

namespace SoundScript.Midi;

public static partial class MidiGenerator
{
    private const int TicksPerQuarterNote = 480;

    public static void Write(MelodyProgram program, IReadOnlyList<TimedNote> timedNotes, string outputPath)
    {
        var midiFile = CreateMidiFile(program, timedNotes);
        midiFile.Write(outputPath, overwriteFile: true);
    }

    private static MidiFile CreateMidiFile(MelodyProgram program, IReadOnlyList<TimedNote> timedNotes)
    {
        var midiFile = new MidiFile();
        var trackChunk = new TrackChunk();

        using (var notesManager = trackChunk.ManageNotes())
        {
            var notes = notesManager.Objects;

            foreach (var timedNote in timedNotes)
            {
                var startTick = (long)(timedNote.StartBeat * TicksPerQuarterNote);
                var lengthTick = (long)(timedNote.DurationMs / 60_000.0 * program.Bpm * TicksPerQuarterNote);
                notes.Add(new Note((SevenBitNumber)timedNote.MidiNumber, lengthTick, startTick));
            }
        }

        midiFile.Chunks.Add(trackChunk);
        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote);
        midiFile.ReplaceTempoMap(TempoMap.Create(Tempo.FromBeatsPerMinute(program.Bpm)));
        return midiFile;
    }
}
