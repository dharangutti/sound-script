// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave.Prosody;

/// <summary>
/// Deterministic rule-based word → canonical phoneme symbol splitter for the
/// v3 prosody proof of concept: scans left to right, consuming known digraphs
/// before single letters, collapsing doubled consonants, and normalising
/// vowels to the canonical long-vowel symbols of
/// <see cref="PhonemeFrequencyTable"/>.
///
/// This deliberately mirrors the grapheme-driven approach of
/// SoundScript.Compose.PhonemeSplitter (prior art in this repo) but is
/// reimplemented here rather than referenced: SoundScript.Wave may depend on
/// SoundScript.Core only (safeguards doc), and SoundScript.Compose pulls in
/// SoundScript.Voice/DryWetMidi. Same guarantees — no dictionaries, no
/// randomness, no culture-sensitive string handling; the same word always
/// yields the same phonemes on every platform.
/// </summary>
internal static class GraphemePhonemeSplitter
{
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
        // vowel digraphs → canonical long-vowel symbols
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

    /// <summary>Splits one word into canonical phoneme symbols.</summary>
    internal static IReadOnlyList<string> Split(string word)
    {
        var phonemes = new List<string>();
        var letters = Normalize(word);
        var index = 0;

        while (index < letters.Length)
        {
            // doubled consonants collapse to one phoneme ("hello" → l, not l l)
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

    // ASCII-only, invariant lower-casing — never culture-sensitive.
    private static string Normalize(string word)
    {
        Span<char> buffer = stackalloc char[word.Length];
        var length = 0;

        foreach (var ch in word)
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
