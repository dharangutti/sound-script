namespace SoundScript.Voice.Phonetics;

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
    private static readonly HashSet<string> LegalOnsets = new(StringComparer.OrdinalIgnoreCase)
    {
        // single consonants are always legal onsets; clusters listed explicitly
        "bl", "br", "ch", "cl", "cr", "dr", "dw", "fl", "fr", "gl", "gr",
        "kl", "kn", "kr", "kw", "ph", "pl", "pr", "qu", "sc", "sh", "sk",
        "sl", "sm", "sn", "sp", "st", "sw", "th", "tr", "tw", "wh", "wr",
        "sch", "scr", "shr", "sph", "spl", "spr", "squ", "str", "thr", "chr"
    };

    /// <summary>Splits a single word into syllables, preserving original casing.</summary>
    public static IReadOnlyList<string> Syllabify(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return [];

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

    /// <summary>Counts syllables without materialising the split.</summary>
    public static int CountSyllables(string word)
    {
        var letters = new string(word.Where(char.IsLetter).ToArray());
        return letters.Length == 0 ? 1 : Math.Max(1, FindNuclei(letters).Count);
    }

    /// <summary>
    /// A nucleus is a maximal vowel group. Handles:
    ///  - silent final e ("shine" → 1 nucleus),
    ///  - consonant + le endings ("twinkle" → the 'le' is a nucleus),
    ///  - y as a vowel when not word-initial ("shiny", "rhythm").
    /// </summary>
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

        // silent final e: "shine", "close" — drop the last nucleus when it is a
        // lone final 'e' preceded by a consonant and another nucleus exists.
        if (nuclei.Count > 1)
        {
            var last = nuclei[^1];
            if (last.Start == last.End
                && last.Start == lower.Length - 1
                && lower[^1] == 'e'
                && !IsVowel(lower, last.Start - 1))
            {
                if (lower[last.Start - 1] == 'l' && last.Start - 2 >= 0 && !IsVowel(lower, last.Start - 2))
                {
                    // consonant + le keeps the nucleus and absorbs the syllabic l:
                    // "twinkle" → twin-kle, "little" → lit-tle, "table" → ta-ble
                    nuclei[^1] = (last.Start - 1, last.End);
                }
                else
                {
                    nuclei.RemoveAt(nuclei.Count - 1);
                }
            }
        }

        return nuclei;
    }

    /// <summary>
    /// For each pair of adjacent nuclei, chooses the split point inside the
    /// intervening consonant cluster using the maximal onset principle.
    /// </summary>
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
                // vowels in hiatus ("cre-ate" style) — split between them
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
        // single intervocalic consonant always opens the next syllable (V-CV)
        if (cluster.Length == 1)
            return 1;

        for (var length = Math.Min(3, cluster.Length); length >= 2; length--)
        {
            var candidate = cluster[^length..];
            if (length < cluster.Length && LegalOnsets.Contains(candidate))
                return length;
        }

        // no legal cluster onset: one consonant to the next syllable,
        // unless the whole cluster is a legal onset digraph kept intact ("th")
        if (LegalOnsets.Contains(cluster))
            return cluster.Length;

        return 1;
    }

    private static List<string> Split(string original, string letters, List<int> letterBoundaries)
    {
        // map boundaries in the letters-only string back onto the original word
        // (apostrophes and other marks stick to the preceding syllable)
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

        // y is a vowel when it is not word-initial and not next to another vowel
        // ("shiny", "rhythm", "my") but a consonant in "yes", "beyond"
        if (c == 'y')
        {
            var prevIsVowel = index > 0 && lower[index - 1] is 'a' or 'e' or 'i' or 'o' or 'u';
            return index > 0 && !prevIsVowel;
        }

        return false;
    }
}
