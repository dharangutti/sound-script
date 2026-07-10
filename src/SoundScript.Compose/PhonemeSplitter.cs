using SoundScript.Wordbank;

namespace SoundScript.Compose;

/// <summary>
/// Deterministic rule-based splitter from a syllable's letters to a flat list
/// of canonical phoneme symbols (the keys of <see cref="PhonemeMapper"/>).
///
/// This is a grapheme-driven approximation, not a dictionary G2P: it scans the
/// syllable left to right, consuming known digraphs first and normalising every
/// remaining letter to a canonical symbol. The same syllable always yields the
/// same phonemes on every platform — no dictionaries, no randomness, no
/// culture-sensitive string handling.
/// </summary>
public static class PhonemeSplitter
{
    private static readonly LocalePack Locale = WordbankCatalog.Default;

    /// <summary>Splits one syllable into canonical phoneme symbols.</summary>
    public static IReadOnlyList<string> Split(string syllable) =>
        GraphemePhonemeEngine.Split(syllable, Locale.GraphemeRules);
}
