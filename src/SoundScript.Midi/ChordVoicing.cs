namespace SoundScript.Midi;

/// <summary>Refines chord voicing to avoid muddy lows and cramped highs.</summary>
internal static class ChordVoicing
{
    internal static (int[] Notes, bool Adjusted) Apply(IReadOnlyList<int> midiNumbers)
    {
        if (midiNumbers.Count == 0)
            return ([], false);

        var notes = midiNumbers.ToArray();
        var adjusted = false;
        var root = notes[0];

        if (root < 40)
        {
            for (var i = 0; i < notes.Length; i++)
                notes[i] += 12;
            adjusted = true;
        }

        if (notes.Length > 3)
        {
            var highestIndex = 0;
            for (var i = 1; i < notes.Length; i++)
            {
                if (notes[i] > notes[highestIndex])
                    highestIndex = i;
            }

            notes[highestIndex] += 12;
            adjusted = true;
        }

        return (notes, adjusted);
    }
}
