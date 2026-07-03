namespace SoundScript.Midi;

/// <summary>Refines chord spacing after initial voicing for clearer harmony.</summary>
internal static class HarmonicSpacing
{
    private const int LowRootThreshold = 40;
    private const int HighNoteThreshold = 84;

    internal static (int[] Notes, bool Adjusted) Apply(IReadOnlyList<int> midiNumbers)
    {
        if (midiNumbers.Count == 0)
            return ([], false);

        var notes = midiNumbers.ToArray();
        var adjusted = false;

        if (notes[0] < LowRootThreshold)
        {
            for (var i = 0; i < notes.Length; i++)
                notes[i] += 12;
            adjusted = true;
        }

        var highestIndex = FindHighestIndex(notes);
        if (notes[highestIndex] > HighNoteThreshold)
        {
            notes[highestIndex] -= 12;
            adjusted = true;
        }

        if (notes.Length > 3)
        {
            highestIndex = FindHighestIndex(notes);
            notes[highestIndex] += 12;
            adjusted = true;
        }

        return (notes, adjusted);
    }

    private static int FindHighestIndex(int[] notes)
    {
        var highestIndex = 0;
        for (var i = 1; i < notes.Length; i++)
        {
            if (notes[i] > notes[highestIndex])
                highestIndex = i;
        }

        return highestIndex;
    }
}
