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

            if (program.TimeSignatureNumerator is not null && program.TimeSignatureDenominator is not null)
            {
                trackChunk.Events.Add(new TimeSignatureEvent(
                    (byte)program.TimeSignatureNumerator.Value,
                    (byte)program.TimeSignatureDenominator.Value));
            }

            var initialPrograms = track.ProgramChanges
                .GroupBy(change => change.Channel)
                .ToDictionary(group => group.Key, group => group.OrderBy(change => change.Beat).First().ProgramNumber);

            if (initialPrograms.Count == 0)
                initialPrograms[DefaultChannel] = InstrumentMap.DefaultProgram;

            foreach (var (channel, programNumber) in initialPrograms.OrderBy(pair => pair.Key))
            {
                trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)programNumber)
                {
                    Channel = (FourBitNumber)channel
                });
            }

            using (var notesManager = trackChunk.ManageNotes())
            {
                var notes = notesManager.Objects;

                foreach (var timedNote in track.Notes)
                {
                    var startTick = (long)(timedNote.StartBeat * TicksPerQuarterNote);
                    var lengthTick = Math.Max(1, (long)(timedNote.DurationBeats * TicksPerQuarterNote));
                    var note = new Note((SevenBitNumber)timedNote.MidiNumber, lengthTick, startTick)
                    {
                        Velocity = (SevenBitNumber)timedNote.Velocity,
                        Channel = (FourBitNumber)timedNote.Channel
                    };
                    notes.Add(note);
                }
            }

            foreach (var programChange in track.ProgramChanges.Where(change => change.Beat > 0))
            {
                var tick = (long)(programChange.Beat * TicksPerQuarterNote);
                trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)programChange.ProgramNumber)
                {
                    DeltaTime = tick,
                    Channel = (FourBitNumber)programChange.Channel
                });
            }

            midiFile.Chunks.Add(trackChunk);
        }

        if (midiFile.Chunks.Count == 0)
            midiFile.Chunks.Add(new TrackChunk());

        midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote);
        midiFile.ReplaceTempoMap(BuildTempoMap(program));
        return midiFile;
    }

    private static TempoMap BuildTempoMap(InterpretedProgram program)
    {
        var timeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote);
        using var manager = new TempoMapManager(timeDivision);

        foreach (var point in program.TempoMap.GetTempoMapPoints().OrderBy(point => point.Beat))
        {
            var tick = (long)Math.Round(point.Beat * TicksPerQuarterNote);
            manager.SetTempo(tick, Tempo.FromBeatsPerMinute(point.Bpm));
        }

        return manager.TempoMap;
    }
}
