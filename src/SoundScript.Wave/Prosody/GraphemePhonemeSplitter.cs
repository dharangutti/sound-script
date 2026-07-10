// UNDER DEVELOPMENT — v3
using SoundScript.Wordbank;

namespace SoundScript.Wave.Prosody;

/// <summary>
/// Deterministic rule-based word → canonical phoneme symbol splitter for the
/// v3 prosody proof of concept. Rules are loaded from the shared wordbank
/// locale pack via <see cref="GraphemePhonemeEngine"/>.
/// </summary>
internal static class GraphemePhonemeSplitter
{
    private static LocalePack Locale => WordbankCatalog.Active;

    /// <summary>
    /// Classifies a sung syllable for timbre selection. A sung note sustains on
    /// its vowel nucleus, so this returns the class of the first vocalic phoneme
    /// when the syllable has one.
    /// </summary>
    internal static PhonemeClass ClassifyLead(string syllable)
    {
        PhonemeClass? lead = null;

        foreach (var phoneme in Split(syllable))
        {
            var phonemeClass = PhonemeFrequencyTable.Lookup(phoneme).Class;
            lead ??= phonemeClass;
            if (phonemeClass == PhonemeClass.Vowel)
                return PhonemeClass.Vowel;
        }

        return lead ?? PhonemeClass.Nasal;
    }

    /// <summary>Splits one word into canonical phoneme symbols.</summary>
    internal static IReadOnlyList<string> Split(string word) =>
        GraphemePhonemeEngine.Split(word, Locale.GraphemeRules);
}
