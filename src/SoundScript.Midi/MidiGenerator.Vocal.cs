using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundScript.Core;

namespace SoundScript.Midi;

public static partial class MidiGenerator
{
    /// <summary>
    /// Vocal tracks use a reserved channel so their Choir/Oohs program never
    /// collides with instrument layers (which allocate channels from 0 upward).
    /// </summary>
    private const byte VocalChannel = 15;

    private static void AppendVocalTracks(MidiFile midiFile, InterpretedProgram program)
    {
        foreach (var vocalTrack in program.VocalTracks)
        {
            if (vocalTrack.Syllables.Count == 0)
                continue;

            var trackChunk = new TrackChunk();
            trackChunk.Events.Add(new SequenceTrackNameEvent(vocalTrack.Name));
            trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)vocalTrack.ProgramNumber)
            {
                Channel = (FourBitNumber)VocalChannel
            });

            using (var notesManager = trackChunk.ManageNotes())
            {
                foreach (var syllable in vocalTrack.Syllables)
                {
                    var startTick = (long)(syllable.StartBeat * TicksPerQuarterNote);
                    var lengthTick = Math.Max(1, (long)(syllable.DurationBeats * TicksPerQuarterNote));
                    notesManager.Objects.Add(new Note((SevenBitNumber)syllable.MidiNumber, lengthTick, startTick)
                    {
                        Velocity = (SevenBitNumber)syllable.Velocity,
                        Channel = (FourBitNumber)VocalChannel
                    });
                }
            }

            using (var eventsManager = trackChunk.ManageTimedEvents())
            {
                foreach (var syllable in vocalTrack.Syllables)
                {
                    if (syllable.IsMelisma)
                        continue;

                    var startTick = (long)(syllable.StartBeat * TicksPerQuarterNote);
                    var text = syllable.IsWordEnd ? syllable.Text + " " : syllable.Text;
                    eventsManager.Objects.Add(new TimedEvent(new LyricEvent(text), startTick));
                }
            }

            midiFile.Chunks.Add(trackChunk);
        }
    }
}
