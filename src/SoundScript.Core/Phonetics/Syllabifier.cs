namespace SoundScript.Core.Phonetics;

using SoundScript.Wordbank;

/// <summary>
/// Deterministic rule-based English syllabifier.
///
/// The engine applies three phonetic principles, in order:
///  1. Nucleus detection — every syllable has exactly one vowel nucleus
///     (vowel group, with silent-e and consonant-le handling).
///  2. Maximal onset — intervocalic consonant clusters attach to the following
///     syllable as far as English phonotactics allow (legal onset table).
///  3. Sonority sequencing — anything that cannot legally start a syllable
///     stays in the coda of the previous one.
///
/// No dictionaries, no randomness: the same word always yields the same
/// syllables on every platform, matching SoundScript's determinism guarantee.
/// </summary>
public static class Syllabifier
{
    private static HashSet<string> LegalOnsets => WordbankCatalog.Active.LegalOnsetSet;

    public static IReadOnlyList<string> Syllabify(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return [];

        if (WordbankCatalog.Active.WordEntryMap.TryGetValue(word, out var entry)
            && entry.Syllables is { Length: > 0 } syllables)
            return ApplyWordEntrySyllables(word, syllables);

        var letters = word.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
            return [word];

        var text = new string(letters);
        var nuclei = FindNuclei(text);
        if (nuclei.Count <= 1)
            return [word];

        var boundaries = FindBoundaries(text, nuclei);
        return Split(word, text, boundaries);
    }

    public static int CountSyllables(string word)
    {
        if (WordbankCatalog.Active.WordEntryMap.TryGetValue(word, out var entry)
            && entry.Syllables is { Length: > 0 } syllables)
            return syllables.Length;

        var letters = new string(word.Where(char.IsLetter).ToArray());
        return letters.Length == 0 ? 1 : Math.Max(1, FindNuclei(letters).Count);
    }

    private static List<(int Start, int End)> FindNuclei(string text)
    {
        var lower = text.ToLowerInvariant();
        var nuclei = new List<(int Start, int End)>();
        var index = 0;

        while (index < lower.Length)
        {
            if (!IsVowel(lower, index))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < lower.Length && IsVowel(lower, index))
                index++;

            nuclei.Add((start, index - 1));
        }

        if (nuclei.Count > 1)
        {
            var last = nuclei[^1];
            if (last.Start == last.End
                && last.Start == lower.Length - 1
                && lower[^1] == 'e'
                && !IsVowel(lower, last.Start - 1))
            {
                if (lower[last.Start - 1] == 'l' && last.Start - 2 >= 0 && !IsVowel(lower, last.Start - 2))
                    nuclei[^1] = (last.Start - 1, last.End);
                else
                    nuclei.RemoveAt(nuclei.Count - 1);
            }
        }

        return nuclei;
    }

    private static List<int> FindBoundaries(string text, List<(int Start, int End)> nuclei)
    {
        var lower = text.ToLowerInvariant();
        var boundaries = new List<int>();

        for (var i = 0; i < nuclei.Count - 1; i++)
        {
            var clusterStart = nuclei[i].End + 1;
            var clusterEnd = nuclei[i + 1].Start - 1;

            if (clusterStart > clusterEnd)
            {
                boundaries.Add(clusterStart);
                continue;
            }

            var cluster = lower[clusterStart..(clusterEnd + 1)];
            var onsetLength = MaximalOnsetLength(cluster);
            boundaries.Add(clusterEnd + 1 - onsetLength);
        }

        return boundaries;
    }

    private static int MaximalOnsetLength(string cluster)
    {
        if (cluster.Length == 1)
            return 1;

        for (var length = Math.Min(3, cluster.Length); length >= 2; length--)
        {
            var candidate = cluster[^length..];
            if (length < cluster.Length && LegalOnsets.Contains(candidate))
                return length;
        }

        if (LegalOnsets.Contains(cluster))
            return cluster.Length;

        return 1;
    }

    private static List<string> Split(string original, string letters, List<int> letterBoundaries)
    {
        var syllables = new List<string>();
        var letterIndex = 0;
        var boundarySet = new HashSet<int>(letterBoundaries);
        var current = new System.Text.StringBuilder();

        foreach (var ch in original)
        {
            if (char.IsLetter(ch))
            {
                if (boundarySet.Contains(letterIndex) && current.Length > 0)
                {
                    syllables.Add(current.ToString());
                    current.Clear();
                }

                letterIndex++;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            syllables.Add(current.ToString());

        return syllables;
    }

    private static bool IsVowel(string lower, int index)
    {
        if (index < 0 || index >= lower.Length)
            return false;

        var c = lower[index];
        if (c is 'a' or 'e' or 'i' or 'o' or 'u')
            return true;

        if (c == 'y')
        {
            var prevIsVowel = index > 0 && lower[index - 1] is 'a' or 'e' or 'i' or 'o' or 'u';
            return index > 0 && !prevIsVowel;
        }

        return false;
    }

    private static IReadOnlyList<string> ApplyWordEntrySyllables(string word, string[] syllables)
    {
        var letters = word.Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
            return syllables;

        var text = new string(letters);
        var expected = string.Concat(syllables);
        if (!string.Equals(expected, text, StringComparison.OrdinalIgnoreCase))
            return syllables;

        var boundaries = new List<int>();
        var offset = 0;
        for (var i = 0; i < syllables.Length - 1; i++)
        {
            offset += syllables[i].Length;
            boundaries.Add(offset);
        }

        return Split(word, text, boundaries);
    }
}
