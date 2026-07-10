using SoundScript.Wave.Io;
using SoundScript.Wordbank;

namespace SoundScript.Vocal;

/// <summary>
/// Per-word synthesis: curated corpus audio first, then rule-based G2P timbre.
/// </summary>
internal static class WordbankVocalSynthesizer
{
    private const double WordGapSeconds = 0.08;

    internal static float[] SynthesizePhrase(string text, VocalEngineOptions options)
    {
        var words = VocalTextSplitter.SplitWords(text);
        if (words.Count == 0)
            throw new InvalidOperationException("Text must contain at least one letter.");

        var locale = ResolveLocale(options);
        var parts = new List<float[]>();

        foreach (var word in words)
        {
            if (parts.Count > 0)
                parts.Add(VocalStemProcessor.Silence(WordGapSeconds));

            parts.Add(PrepareWordStem(SynthesizeWord(word, locale)));
        }

        return VocalStemProcessor.Concat(parts);
    }

    /// <summary>Corpus human audio when available; otherwise G2P timbre (wordbank-only path).</summary>
    internal static float[] SynthesizeWord(string word, string locale)
    {
        if (TrySynthesizeCorpusWord(word, locale, out var audio))
            return audio;

        var g2p = TimbreVocalSynthesizer.SynthesizeWord(word);
        if (g2p.Length > 0 && VocalStemNormalizer.Peak(g2p) > 1e-6)
            return g2p;

        throw new InvalidOperationException(
            $"Wordbank engine could not synthesize '{word}' — no corpus audio and G2P produced silence.");
    }

    internal static bool TrySynthesizeCorpusWord(string word, string locale, out float[] audio)
    {
        audio = [];

        if (!CorpusCatalog.TryGetLemma(locale, word, out var entry) || string.IsNullOrWhiteSpace(entry.Audio))
            return false;

        var path = CorpusCatalog.ResolveAudioPath(entry);
        if (path is null)
            return false;

        audio = WavReader.ReadMono(path);
        audio = VocalStemProcessor.ApplyTransform(
            audio,
            entry.TrimStartMs,
            entry.TrimEndMs,
            entry.Gain,
            entry.PitchSemitones);

        return audio.Length > 0 && VocalStemNormalizer.Peak(audio) > 1e-6;
    }

    internal static float[] PrepareWordStem(float[] samples) =>
        VocalStemNormalizer.Normalize(samples, outputGain: 1.0);

    internal static string ResolveLocale(VocalEngineOptions options) =>
        !string.IsNullOrWhiteSpace(options.Locale)
            ? options.Locale
            : WordbankCatalog.ActiveLocaleCode;
}
