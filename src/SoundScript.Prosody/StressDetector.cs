using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Lightweight, deterministic rule-based English stress heuristic — not a
/// pronunciation dictionary. Assigns one <see cref="StressLevel"/> per
/// syllable of a word:
///
///  - 1 syllable: <see cref="StressLevel.Primary"/> for content words,
///    <see cref="StressLevel.Unstressed"/> for function words.
///  - 2 syllables: trochaic by default (syllable 0 is primary), unless the
///    word starts with a common unstressed prefix (re-, de-, un-, in-, con-,
///    dis-, ex-, en-), in which case syllable 1 is primary.
///  - 3+ syllables: first syllable primary, last syllable secondary, every
///    syllable in between unstressed.
///
/// Per-word overrides from the wordbank dictionary are applied when present.
/// Same word and syllable split always yields the same stress pattern.
/// </summary>
public static class StressDetector
{
    private static readonly string[] UnstressedPrefixes = WordbankCatalog.Default.StressPrefixes.Prefixes;

    /// <summary>Detects one stress level per syllable.</summary>
    public static IReadOnlyList<StressLevel> Detect(string word, IReadOnlyList<string> syllables)
    {
        if (syllables.Count == 0)
            return [];

        if (WordbankCatalog.Default.WordEntryMap.TryGetValue(word, out var entry)
            && entry.Stress is { Length: > 0 } stressOverride)
            return ParseStress(stressOverride);

        var isFunctionWord = FunctionWords.Contains(word);

        if (syllables.Count == 1)
            return [isFunctionWord ? StressLevel.Unstressed : StressLevel.Primary];

        if (syllables.Count == 2)
        {
            var primaryIndex = StartsWithUnstressedPrefix(syllables[0]) ? 1 : 0;
            return primaryIndex == 0
                ? [StressLevel.Primary, StressLevel.Unstressed]
                : [StressLevel.Unstressed, StressLevel.Primary];
        }

        var levels = new StressLevel[syllables.Count];
        levels[0] = StressLevel.Primary;
        for (var i = 1; i < levels.Length - 1; i++)
            levels[i] = StressLevel.Unstressed;
        levels[^1] = StressLevel.Secondary;
        return levels;
    }

    private static IReadOnlyList<StressLevel> ParseStress(string[] stress) =>
        stress.Select(ParseStressLevel).ToArray();

    private static StressLevel ParseStressLevel(string value) => value switch
    {
        "primary" => StressLevel.Primary,
        "secondary" => StressLevel.Secondary,
        _ => StressLevel.Unstressed,
    };

    private static bool StartsWithUnstressedPrefix(string syllable)
    {
        foreach (var prefix in UnstressedPrefixes)
        {
            if (syllable.Length > prefix.Length
                && string.Compare(syllable, 0, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0)
                return true;
        }

        return false;
    }
}
