using SoundScript.Wordbank.Models;

namespace SoundScript.Wordbank;

/// <summary>
/// Shared grapheme-to-phoneme splitter driven by wordbank JSON rules.
/// Used by Compose and Wave so both engines stay aligned.
/// </summary>
public static class GraphemePhonemeEngine
{
    public static IReadOnlyList<string> Split(string text, GraphemeRulesDocument rules) =>
        Split(text, rules.Digraphs, rules.SingleLetters);

    public static IReadOnlyList<string> Split(
        string text,
        IReadOnlyList<GraphemeRule> digraphs,
        IReadOnlyList<SingleLetterRule> singleLetters)
    {
        var phonemes = new List<string>();
        var letters = Normalize(text);
        var index = 0;

        while (index < letters.Length)
        {
            if (index + 1 < letters.Length
                && letters[index] == letters[index + 1]
                && !IsVowelLetter(letters[index]))
            {
                index++;
                continue;
            }

            var digraph = MatchDigraph(letters, index, digraphs);
            if (digraph is not null)
            {
                phonemes.AddRange(digraph);
                index += 2;
                continue;
            }

            phonemes.AddRange(MapSingleLetter(letters[index], singleLetters));
            index++;
        }

        return phonemes;
    }

    private static string Normalize(string text)
    {
        Span<char> buffer = stackalloc char[text.Length];
        var length = 0;

        foreach (var ch in text)
        {
            if (ch is >= 'a' and <= 'z')
                buffer[length++] = ch;
            else if (ch is >= 'A' and <= 'Z')
                buffer[length++] = (char)(ch + ('a' - 'A'));
        }

        return new string(buffer[..length]);
    }

    private static string[]? MatchDigraph(string letters, int index, IReadOnlyList<GraphemeRule> digraphs)
    {
        if (index + 1 >= letters.Length)
            return null;

        foreach (var rule in digraphs)
        {
            var grapheme = rule.Grapheme;
            if (grapheme.Length < 2)
                continue;

            if (letters[index] == grapheme[0] && letters[index + 1] == grapheme[1])
                return rule.Phonemes;
        }

        return null;
    }

    private static string[] MapSingleLetter(char letter, IReadOnlyList<SingleLetterRule> singleLetters)
    {
        foreach (var rule in singleLetters)
        {
            if (rule.Letter.Length == 1 && rule.Letter[0] == letter)
                return rule.Phonemes;
        }

        return [letter.ToString()];
    }

    private static bool IsVowelLetter(char c) =>
        c is 'a' or 'e' or 'i' or 'o' or 'u';
}
