using SoundScript.Voice.Phonetics;

namespace SoundScript.Prosody;

/// <summary>
/// Splits text into words, then each word into syllables via the existing
/// deterministic <see cref="Syllabifier"/> — the same tokenization the V3.1
/// <c>PhonemeComposer</c> performs, but keeping word boundaries (which
/// <c>PhonemeComposer.SplitSyllables</c> flattens away) since word-level
/// prosody needs to know which syllables belong to which word.
/// </summary>
public static class WordTokenizer
{
    /// <summary>Splits text into words, each carrying its syllable breakdown.</summary>
    public static IReadOnlyList<WordUnit> Tokenize(string text)
    {
        var units = new List<WordUnit>();
        foreach (var word in SplitWords(text))
            units.Add(new WordUnit(word, Syllabifier.Syllabify(word)));

        return units;
    }

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(text))
            return words;

        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var isWordChar = char.IsLetter(text[i]) || text[i] == '\'';
            if (isWordChar && start < 0)
                start = i;
            else if (!isWordChar && start >= 0)
            {
                words.Add(text[start..i]);
                start = -1;
            }
        }

        if (start >= 0)
            words.Add(text[start..]);

        return words;
    }
}
