namespace SoundScript.Voice.Phonetics;

/// <summary>One syllable of a lyric line, ready to bind to a note.</summary>
public readonly record struct LyricSyllable(string Text, bool IsWordEnd);

/// <summary>
/// Turns a lyric line into an ordered syllable stream and aligns it to a note
/// count, following vocal-writing conventions:
///  - one syllable per note when counts match,
///  - melisma (held vowel) when there are more notes than syllables,
///  - overflow syllables merge into the final note when there are more
///    syllables than notes (reported as a warning by the interpreter).
/// </summary>
public static class LyricAligner
{
    public static IReadOnlyList<LyricSyllable> ToSyllables(string lyric)
    {
        var result = new List<LyricSyllable>();

        foreach (var word in lyric.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var syllables = Syllabifier.Syllabify(word);
            for (var i = 0; i < syllables.Count; i++)
                result.Add(new LyricSyllable(syllables[i], i == syllables.Count - 1));
        }

        return result;
    }

    /// <summary>
    /// Aligns syllables to <paramref name="noteCount"/> slots. Slots past the
    /// syllable stream are melisma continuations (empty text). If the stream is
    /// longer than the slots, the tail is joined into the last slot and
    /// <paramref name="overflowed"/> is set.
    /// </summary>
    public static IReadOnlyList<LyricSyllable?> Align(
        IReadOnlyList<LyricSyllable> syllables,
        int noteCount,
        out bool overflowed)
    {
        overflowed = syllables.Count > noteCount;
        var slots = new LyricSyllable?[noteCount];

        for (var i = 0; i < noteCount; i++)
        {
            if (i < syllables.Count)
                slots[i] = syllables[i];
        }

        if (overflowed && noteCount > 0)
        {
            var tail = syllables.Skip(noteCount - 1).ToList();
            var joined = string.Concat(tail.Select(s => s.Text));
            slots[noteCount - 1] = new LyricSyllable(joined, tail[^1].IsWordEnd);
        }

        return slots;
    }
}
