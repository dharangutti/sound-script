using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Closed, deterministic set of common English function words (articles,
/// prepositions, conjunctions, pronouns, auxiliaries/copulas). Any word not
/// in this set is treated as a content word. Pure data — no dictionaries, no
/// randomness, no culture-sensitive lookups.
/// </summary>
public static class FunctionWords
{
    private static readonly HashSet<string> Words = WordbankCatalog.Default.FunctionWordSet;

    /// <summary>True when <paramref name="word"/> is a closed-class function word.</summary>
    public static bool Contains(string word) => Words.Contains(word);
}
