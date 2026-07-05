namespace SoundScript.Prosody;

/// <summary>
/// Closed, deterministic set of common English function words (articles,
/// prepositions, conjunctions, pronouns, auxiliaries/copulas). Any word not
/// in this set is treated as a content word. Pure data — no dictionaries, no
/// randomness, no culture-sensitive lookups.
/// </summary>
public static class FunctionWords
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        // articles
        "a", "an", "the",
        // conjunctions
        "and", "or", "but", "nor", "so", "yet",
        // prepositions
        "in", "on", "at", "to", "of", "for", "with", "by", "from", "as",
        "into", "onto", "over", "under", "about", "up", "down", "off",
        // pronouns
        "i", "you", "he", "she", "it", "we", "they", "me", "him", "her",
        "us", "them", "this", "that", "these", "those",
        // auxiliaries / copulas / modals
        "is", "are", "was", "were", "be", "been", "being", "am",
        "do", "does", "did", "has", "have", "had",
        "will", "would", "can", "could", "shall", "should", "may", "might", "must"
    };

    /// <summary>True when <paramref name="word"/> is a closed-class function word.</summary>
    public static bool Contains(string word) => Words.Contains(word);
}
