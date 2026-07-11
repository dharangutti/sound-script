using SoundScript.Timbre;
using SoundScript.Vocal.Wordbank;
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

        if (options.Continuous)
        {
            var stems = new List<float[]>(words.Count);
            foreach (var word in words)
                stems.Add(PrepareWordStem(SynthesizeWord(word, locale)));

            return ContinuousVocalRenderer.Assemble(words, stems, options);
        }

        var parts = new List<float[]>();
        foreach (var word in words)
        {
            if (parts.Count > 0)
                parts.Add(VocalStemProcessor.Silence(WordGapSeconds));

            var stem = PrepareWordStem(SynthesizeWord(word, locale));
            parts.Add(ApplyWordTransform(stem, word, options));
        }

        return VocalStemProcessor.Concat(parts);
    }

    /// <summary>
    /// Applies word-level SoundCSS transforms to a prepared stem when the options
    /// carry a matching pronunciation rule. The canonical base pitch is estimated
    /// from the stem (Prompt 1 metadata) so pitch transforms stay relative to the
    /// recorded voice. Deterministic: no-op when no rule matches.
    /// </summary>
    internal static float[] ApplyWordTransform(float[] stem, string word, VocalEngineOptions options)
    {
        if (options.Pronunciations is null || stem.Length == 0)
            return stem;

        if (!options.Pronunciations.TryGetValue(word, out var pronunciation))
            return stem;

        var basePitch = AudioNormalizeOps.DetectBasePitchHz(stem, WavWriter.SampleRate);
        var metadata = basePitch > 0
            ? CanonicalVoiceMetadata.Default with { BasePitchHz = basePitch }
            : CanonicalVoiceMetadata.Default;

        var plan = SoundCssDspMapper.Map(pronunciation, metadata);
        var transformed = DspTransformRenderer.Render(stem, plan, options.Seed);
        return transformed.Length > 0 ? transformed : stem;
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

        // Bytes-based read works for both the on-disk corpus (CLI) and an
        // in-memory corpus fetched at runtime (WebAssembly playground).
        if (!CorpusCatalog.TryGetAudioBytes(entry, out var wavBytes))
            return false;

        using var stream = new MemoryStream(wavBytes);
        audio = WavReader.ReadMono(stream);
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
