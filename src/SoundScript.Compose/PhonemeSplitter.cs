namespace SoundScript.Compose;

/// <summary>
/// Deterministic rule-based splitter from a syllable's letters to a flat list
/// of canonical phoneme symbols (the keys of <see cref="PhonemeMapper"/>).
///
/// This is a grapheme-driven approximation, not a dictionary G2P: it scans the
/// syllable left to right, consuming known digraphs first and normalising every
/// remaining letter to a canonical symbol. The same syllable always yields the
/// same phonemes on every platform — no dictionaries, no randomness, no
/// culture-sensitive string handling.
/// </summary>
public static class PhonemeSplitter
{
    /// <summary>
    /// Two-letter graphemes recognised before single letters, normalised to
    /// canonical phoneme symbols. Pure data; extend by adding rows.
    /// </summary>
    private static readonly (string Grapheme, string[] Phonemes)[] Digraphs =
    [
        // consonant digraphs
        ("sh", ["sh"]),
        ("ch", ["ch"]),
        ("th", ["th"]),
        ("ph", ["f"]),
        ("wh", ["w"]),
        ("ng", ["ng"]),
        ("ck", ["k"]),
        ("qu", ["k", "w"]),
        // vowel digraphs (canonical long-vowel symbols)
        ("aa", ["aa"]),
        ("ee", ["ee"]),
        ("oo", ["oo"]),
        ("ai", ["ai"]),
        ("au", ["au"]),
        ("ea", ["ee"]),
        ("ey", ["ee"]),
        ("ie", ["ee"]),
        ("ay", ["ai"]),
        ("oa", ["oo"]),
        ("ou", ["au"]),
        ("ow", ["au"]),
    ];

    /// <summary>
    /// Single-letter fallbacks. Vowels normalise to the canonical long-vowel
    /// symbols of the gesture table; c/q collapse to /k/, x expands to /k s/.
    /// Letters not listed here map to themselves (b → "b", m → "m", ...).
    /// </summary>
    private static readonly (char Letter, string[] Phonemes)[] SingleLetters =
    [
        ('a', ["aa"]),
        ('e', ["ee"]),
        ('i', ["ai"]),
        ('o', ["au"]),
        ('u', ["oo"]),
        ('y', ["ee"]),
        ('c', ["k"]),
        ('q', ["k"]),
        ('x', ["k", "s"]),
    ];

    /// <summary>Splits one syllable into canonical phoneme symbols.</summary>
    public static IReadOnlyList<string> Split(string syllable)
    {
        var phonemes = new List<string>();
        if (string.IsNullOrEmpty(syllable))
            return phonemes;

        var letters = Normalize(syllable);
        var index = 0;

        while (index < letters.Length)
        {
            // doubled consonants collapse to one phoneme ("little" → lit-tle → t)
            if (index + 1 < letters.Length
                && letters[index] == letters[index + 1]
                && !IsVowelLetter(letters[index]))
            {
                index++;
                continue;
            }

            var digraph = MatchDigraph(letters, index);
            if (digraph is not null)
            {
                phonemes.AddRange(digraph);
                index += 2;
                continue;
            }

            phonemes.AddRange(MapSingleLetter(letters[index]));
            index++;
        }

        return phonemes;
    }

    private static string Normalize(string syllable)
    {
        Span<char> buffer = stackalloc char[syllable.Length];
        var length = 0;

        foreach (var ch in syllable)
        {
            if (ch is >= 'a' and <= 'z')
                buffer[length++] = ch;
            else if (ch is >= 'A' and <= 'Z')
                buffer[length++] = (char)(ch + ('a' - 'A'));
        }

        return new string(buffer[..length]);
    }

    private static string[]? MatchDigraph(string letters, int index)
    {
        if (index + 1 >= letters.Length)
            return null;

        foreach (var (grapheme, mapped) in Digraphs)
        {
            if (letters[index] == grapheme[0] && letters[index + 1] == grapheme[1])
                return mapped;
        }

        return null;
    }

    private static string[] MapSingleLetter(char letter)
    {
        foreach (var (candidate, phonemes) in SingleLetters)
        {
            if (candidate == letter)
                return phonemes;
        }

        return [letter.ToString()];
    }

    private static bool IsVowelLetter(char c) =>
        c is 'a' or 'e' or 'i' or 'o' or 'u';
}
