// UNDER DEVELOPMENT — v4
namespace SoundScript.Wave.Prosody;

/// <summary>
/// Deterministic, dictionary-free syllable splitter for the v4 <c>sing</c>
/// directive: aligns a lyric string against a <c>sing</c> line's explicit
/// per-syllable pitches. Same philosophy as <see cref="GraphemePhonemeSplitter"/>
/// (rule-based, no lookup tables, no randomness, ASCII/invariant-only) — it
/// counts syllables rather than mapping phonemes.
///
/// The heuristic is a vowel-cluster count: each maximal run of vowel letters
/// (including <c>y</c>) is one syllable nucleus, and the word is cut so a
/// single consonant preceding a nucleus becomes that syllable's onset (the
/// "maximal onset" convention — <c>wonder</c> → <c>won·der</c>,
/// <c>twinkle</c> → <c>twink·le</c>). This is not linguistically perfect; it
/// is deterministic and matches the syllable count in the common case, which
/// is all the alignment needs.
/// </summary>
internal static class SyllableSplitter
{
    /// <summary>
    /// Splits a lyric line into syllable substrings, in order, across all its
    /// words. A word with no vowel letters counts as a single syllable.
    /// </summary>
    internal static IReadOnlyList<string> Split(string lyric)
    {
        var syllables = new List<string>();
        foreach (var word in SplitWords(lyric))
            SplitWord(word, syllables);

        return syllables;
    }

    private static void SplitWord(string word, List<string> syllables)
    {
        var nucleusStarts = VowelClusterStarts(word);

        // No vowel (or a single nucleus): the whole word is one syllable.
        if (nucleusStarts.Count <= 1)
        {
            syllables.Add(word);
            return;
        }

        var cut = 0;
        for (var i = 1; i < nucleusStarts.Count; i++)
        {
            // The single consonant immediately before the next nucleus becomes
            // that syllable's onset; the boundary falls just before it. Distinct
            // nuclei always have at least one consonant between them (contiguous
            // vowels collapse into one cluster), so the boundary strictly
            // advances and every substring is non-empty.
            var boundary = nucleusStarts[i] - 1;
            syllables.Add(word[cut..boundary]);
            cut = boundary;
        }

        syllables.Add(word[cut..]);
    }

    private static List<int> VowelClusterStarts(string word)
    {
        var starts = new List<int>();
        var inCluster = false;

        for (var i = 0; i < word.Length; i++)
        {
            if (IsVowelLetter(word[i]))
            {
                if (!inCluster)
                    starts.Add(i);
                inCluster = true;
            }
            else
            {
                inCluster = false;
            }
        }

        return starts;
    }

    // Mirrors ProsodyToneGenerator.SplitWords: contiguous letter runs only,
    // so punctuation/whitespace never leaks into a syllable.
    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var isLetter = i < text.Length && char.IsLetter(text[i]);
            if (isLetter && start < 0)
            {
                start = i;
            }
            else if (!isLetter && start >= 0)
            {
                words.Add(text[start..i]);
                start = -1;
            }
        }

        return words;
    }

    // 'y' counts as a vowel (matching GraphemePhonemeSplitter's vowel mapping),
    // ASCII-only and case-insensitive without culture-sensitive comparison.
    private static bool IsVowelLetter(char c)
    {
        var lower = c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;
        return lower is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';
    }
}
