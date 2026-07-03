using SoundScript.Core.Ast;

namespace SoundScript.Midi;

/// <summary>Advanced chord voicing options applied after Phase 1 voicing.</summary>
internal static class AdvancedChordVoicing
{
    internal static (int[] Notes, bool Adjusted) Apply(IReadOnlyList<int> midiNumbers, ChordVoicingStyle? voicing)
    {
        if (voicing is null || midiNumbers.Count == 0)
            return (midiNumbers.ToArray(), false);

        var notes = midiNumbers.OrderBy(note => note).ToArray();
        var adjusted = true;

        notes = voicing switch
        {
            ChordVoicingStyle.Drop2 => ApplyDrop(notes, 2),
            ChordVoicingStyle.Drop3 => ApplyDrop(notes, 3),
            ChordVoicingStyle.Inversion1 => ApplyInversion(notes, 1),
            ChordVoicingStyle.Inversion2 => ApplyInversion(notes, 2),
            ChordVoicingStyle.Spread => ApplySpread(notes),
            _ => notes
        };

        return (notes, adjusted);
    }

    private static int[] ApplyDrop(int[] notes, int voiceFromTop)
    {
        if (notes.Length < voiceFromTop)
            return notes;

        var sorted = (int[])notes.Clone();
        Array.Sort(sorted);
        sorted[^voiceFromTop] -= 12;
        Array.Sort(sorted);
        return sorted;
    }

    private static int[] ApplyInversion(int[] notes, int inversion)
    {
        if (inversion <= 0 || notes.Length < 2)
            return notes;

        var sorted = (int[])notes.Clone();
        Array.Sort(sorted);
        var count = Math.Min(inversion, sorted.Length - 1);

        for (var i = 0; i < count; i++)
            sorted[i] += 12;

        Array.Sort(sorted);
        return sorted;
    }

    private static int[] ApplySpread(int[] notes)
    {
        if (notes.Length < 2)
            return notes;

        var sorted = (int[])notes.Clone();
        Array.Sort(sorted);

        for (var i = 1; i < sorted.Length; i++)
            sorted[i] += 12;

        return sorted;
    }
}
