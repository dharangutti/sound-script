using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;

namespace SoundScript.Midi;

public static partial class MidiGenerator
{
    private const int TicksPerQuarterNote = 480;
    private const byte DefaultChannel = 0;

    public static void Write(InterpretedProgram program, string outputPath)
    {
        var midiFile = CreateMidiFile(program);
        midiFile.Write(outputPath, overwriteFile: true);
    }

    private static MidiFile CreateMidiFile(InterpretedProgram program)
    {
        var midiFile = new MidiFile();

        foreach (var track in program.Tracks)
        {
            var trackChunk = new TrackChunk();
            var initialProgram = track.ProgramChanges.Count > 0
                ? track.ProgramChanges[0].ProgramNumber
                : InstrumentMap.DefaultProgram;

            trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)initialProgram)
            {
                Channel = (FourBitNumber)DefaultChannel
            });

            if (program.TimeSignatureNumerator is not null && program.TimeSignatureDenominator is not null)
            {
                trackChunk.Events.Add(new TimeSignatureEvent(
                    (byte)program.TimeSignatureNumerator.Value,
                    (byte)program.TimeSignatureDenominator.Value));
            }

            using (var notesManager = trackChunk.ManageNotes())
            {
                var notes = notesManager.Objects;

                foreach (var timedNote in track.Notes)
                {
                    var startTick = (long)(timedNote.StartBeat * TicksPerQuarterNote);
                    var lengthTick = Math.Max(1, (long)(timedNote.DurationMs / 60_000.0 * program.Tempo * TicksPerQuarterNote));
                    var note = new Note((SevenBitNumber)timedNote.MidiNumber, lengthTick, startTick)
                    {
                        Velocity = (SevenBitNumber)timedNote.Velocity
                    };
                    notes.Add(note);
                }
            }

            foreach (var programChange in track.ProgramChanges.Skip(1))
            {
                var tick = (long)(programChange.Beat * TicksPerQuarterNote);
                trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)programChange.ProgramNumber)
                {
                    DeltaTime = tick,
                    Channel = (FourBitNumber)DefaultChannel
                });
            }

            midiFile.Chunks.Add(trackChunk);
        }

        if (midiFile.Chunks.Count == 0)
            midiFile.Chunks.Add(new TrackChunk());

        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote);
        midiFile.ReplaceTempoMap(TempoMap.Create(Tempo.FromBeatsPerMinute(program.Tempo)));
        return midiFile;
    }
}
