namespace SoundScript.Midi;

/// <summary>Balances chord voice velocities for clearer harmony.</summary>
internal static class ChordBalancer
{
    internal static (int[] Velocities, bool Balanced) Apply(IReadOnlyList<int> midiNotes, int baseVelocity)
    {
        if (midiNotes.Count == 0)
            return ([], false);

        var velocities = Enumerable.Repeat(baseVelocity, midiNotes.Count).ToArray();
        var rootIndex = 0;
        var topIndex = FindHighestIndex(midiNotes);

        velocities[rootIndex] = Math.Min(127, velocities[rootIndex] + 8);
        velocities[topIndex] = Math.Min(127, velocities[topIndex] + 4);

        for (var i = 0; i < velocities.Length; i++)
        {
            if (i == rootIndex || i == topIndex)
                continue;

            velocities[i] = Math.Max(1, velocities[i] - 5);
        }

        return (velocities, true);
    }

    private static int FindHighestIndex(IReadOnlyList<int> midiNotes)
    {
        var highestIndex = 0;
        for (var i = 1; i < midiNotes.Count; i++)
        {
            if (midiNotes[i] > midiNotes[highestIndex])
                highestIndex = i;
        }

        return highestIndex;
    }
}
