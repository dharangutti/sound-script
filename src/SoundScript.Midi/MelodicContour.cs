using SoundScript.Core.Notation;

namespace SoundScript.Midi;

/// <summary>Corrects wide melodic leaps by a single octave displacement.</summary>
internal static class MelodicContour
{
    private const int MaxLeap = 7;

    internal static (NotatedNote Note, bool Adjusted) Apply(int? previousMidi, NotatedNote current)
    {
        if (previousMidi is null)
            return (current, false);

        var midi = current.ResolvedMidiNumber;
        var interval = midi - previousMidi.Value;
        if (interval is 0 or 12 or -12)
            return (current, false);

        if (interval > MaxLeap)
            return (current.WithAdjustedMidi(midi - 12), true);

        if (interval < -MaxLeap)
            return (current.WithAdjustedMidi(midi + 12), true);

        return (current, false);
    }
}
