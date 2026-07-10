using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Computes the phrase-level pitch contour: a small per-word semitone delta
/// (added on top of each word's <see cref="WordPitchTable"/> base pitch) that
/// shapes the whole phrase into a rising, falling, or stepped arc. Purely
/// deterministic — a linear ramp indexed by word position, no randomness.
/// </summary>
public static class PhraseContourEngine
{
    private static Wordbank.Models.WordProsodyDocument Prosody => WordbankCatalog.Active.WordProsody;

    /// <summary>
    /// Detects sentence type from surface punctuation: a trailing '?' is a
    /// question, everything else is treated as a statement. Multi-sentence
    /// text is treated as a single contour arc — sentence splitting is out of
    /// scope for this heuristic.
    /// </summary>
    public static SentenceType DetectSentenceType(string text)
    {
        var trimmed = text.TrimEnd();
        return trimmed.EndsWith('?') ? SentenceType.Question : SentenceType.Statement;
    }

    /// <summary>Computes one semitone delta per word index, shaping the phrase contour.</summary>
    public static IReadOnlyList<int> ComputeDeltas(int wordCount, SentenceType type)
    {
        var deltas = new int[wordCount];
        if (wordCount == 0)
            return deltas;

        for (var i = 0; i < wordCount; i++)
        {
            deltas[i] = type switch
            {
                SentenceType.Question => Ramp(i, wordCount, Prosody.PhraseContours["question"]),
                SentenceType.ListItem => ComputeListItemDelta(i, Prosody.PhraseContours["listItem"]),
                _ => Ramp(i, wordCount, Prosody.PhraseContours["statement"]),
            };
        }

        return deltas;
    }

    private static int Ramp(int index, int count, Wordbank.Models.RampContour contour)
    {
        if (count <= 1)
            return 0;

        var t = (double)index / (count - 1);
        var value = contour.From + (contour.To - contour.From) * t;
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static int ComputeListItemDelta(int index, Wordbank.Models.RampContour contour)
    {
        var step = contour.Step ?? 1;
        var max = contour.Max ?? 3;
        return Math.Min(index * step, max);
    }
}
