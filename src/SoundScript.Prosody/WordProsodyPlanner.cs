namespace SoundScript.Prosody;

/// <summary>One word's prosody inputs: its base pitch, category, and per-syllable stress.</summary>
public readonly record struct WordProsodyPlan(
    int BaseMidi,
    WordCategory Category,
    IReadOnlyList<StressLevel> Stress);

/// <summary>
/// Plans the base (word-level) pitch for every word in a phrase: classifies
/// each word as content or function (<see cref="FunctionWords"/>), detects its
/// phrase position (start/middle/end), looks up its stress pattern
/// (<see cref="StressDetector"/>), and resolves a base MIDI pitch from
/// <see cref="WordPitchTable"/>. Deterministic — the same word sequence always
/// yields the same plan.
/// </summary>
public static class WordProsodyPlanner
{
    /// <summary>Plans base prosody for every word in the given sequence.</summary>
    public static IReadOnlyList<WordProsodyPlan> Plan(IReadOnlyList<WordUnit> words)
    {
        var plans = new List<WordProsodyPlan>(words.Count);

        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var category = ResolveCategory(word.Word);
            var position = PositionOf(i, words.Count);
            var stress = StressDetector.Detect(word.Word, word.Syllables);
            var baseMidi = WordPitchTable.BaseMidi(category, position);

            plans.Add(new WordProsodyPlan(baseMidi, category, stress));
        }

        return plans;
    }

    private static WordCategory ResolveCategory(string word)
    {
        if (Wordbank.WordbankCatalog.Active.WordEntryMap.TryGetValue(word, out var entry)
            && entry.Category is not null)
        {
            return entry.Category.Equals("function", StringComparison.OrdinalIgnoreCase)
                ? WordCategory.Function
                : WordCategory.Content;
        }

        return FunctionWords.Contains(word) ? WordCategory.Function : WordCategory.Content;
    }

    private static PhrasePosition PositionOf(int index, int count)
    {
        if (count <= 1)
            return PhrasePosition.Start;
        if (index == 0)
            return PhrasePosition.Start;
        return index == count - 1 ? PhrasePosition.End : PhrasePosition.Middle;
    }
}
