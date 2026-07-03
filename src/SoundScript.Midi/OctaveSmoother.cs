using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Reduces extreme octave jumps while preserving pitch spelling.</summary>
internal static class OctaveSmoother
{
    internal static (NotatedNote Note, bool Adjusted) Apply(int? previousMidi, NotatedNote current)
    {
        if (previousMidi is null)
            return (current, false);

        var midi = current.ResolvedMidiNumber;
        var adjusted = false;

        while (midi - previousMidi.Value > 12)
        {
            midi -= 12;
            adjusted = true;
        }

        while (midi - previousMidi.Value < -12)
        {
            midi += 12;
            adjusted = true;
        }

        return adjusted ? (current.WithAdjustedMidi(midi), true) : (current, false);
    }
}
