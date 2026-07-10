using SoundScript.Wave.Io;

namespace SoundScript.Vocal;

/// <summary>
/// Deterministic offline vocal engine: curated corpus human audio with
/// rule-based G2P timbre fallback. Does not use eSpeak.
/// </summary>
public sealed class WordbankVocalEngine : IVocalEngine
{
    public string Name => "wordbank";

    public void Synthesize(string text, string outputWavPath, VocalEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var samples = WordbankVocalSynthesizer.SynthesizePhrase(text, options);
        samples = VocalStemNormalizer.Normalize(samples, options.OutputGain);

        if (VocalStemNormalizer.Peak(samples) <= 1e-6)
        {
            throw new InvalidOperationException(
                "Wordbank engine produced a silent stem — check corpus audio and text.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputWavPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        WavWriter.Write(outputWavPath, samples);
    }
}
