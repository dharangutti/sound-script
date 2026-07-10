using SoundScript.Timbre;

namespace SoundScript.Vocal;

/// <summary>Rule-based G2P vocal synthesis via wordbank timbre tables.</summary>
internal static class TimbreVocalSynthesizer
{
    internal static float[] SynthesizeWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return [];

        var phonemes = PhonemeTimbreMapper.PhonemesFromText(word);
        if (phonemes.Count == 0)
            return [];

        var timeline = PhonemeVocalTimeline.Build(phonemes);
        return SpectralEngine.Synthesize(timeline);
    }
}
