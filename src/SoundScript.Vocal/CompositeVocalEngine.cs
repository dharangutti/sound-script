using SoundScript.Wave.Io;
using SoundScript.Wave.Prosody;

namespace SoundScript.Vocal;

/// <summary>
/// Wordbank-first vocal engine: corpus human audio → G2P timbre → eSpeak → prosody.
/// </summary>
public sealed class CompositeVocalEngine : IVocalEngine
{
    private readonly EspeakNgVocalEngine? _espeak;
    private readonly ProsodyVocalEngine _prosody = new();

    public CompositeVocalEngine()
    {
        _espeak = EspeakNgVocalEngine.ResolveExecutable() is not null
            ? new EspeakNgVocalEngine()
            : null;
    }

    public string Name => "composite";

    public void Synthesize(string text, string outputWavPath, VocalEngineOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var words = VocalTextSplitter.SplitWords(text);
        if (words.Count == 0)
            throw new InvalidOperationException("Text must contain at least one letter.");

        var locale = WordbankVocalSynthesizer.ResolveLocale(options);
        var parts = new List<float[]>();

        foreach (var word in words)
        {
            if (parts.Count > 0)
                parts.Add(VocalStemProcessor.Silence(WordGapSeconds));

            parts.Add(SynthesizeWordWithFallback(word, locale, options));
        }

        var samples = VocalStemProcessor.Concat(parts);
        samples = VocalStemNormalizer.Normalize(samples, options.OutputGain);

        if (VocalStemNormalizer.Peak(samples) <= 1e-6)
            throw new InvalidOperationException("Composite engine produced a silent stem.");

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputWavPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        WavWriter.Write(outputWavPath, samples);
    }

    private const double WordGapSeconds = 0.08;

    private float[] SynthesizeWordWithFallback(string word, string locale, VocalEngineOptions options)
    {
        try
        {
            return WordbankVocalSynthesizer.SynthesizeWord(word, locale, options);
        }
        catch (InvalidOperationException)
        {
            // fall through to espeak / prosody
        }

        if (_espeak is not null)
        {
            try
            {
                var espeakOptions = new VocalEngineOptions
                {
                    Voice = options.Voice,
                    Locale = options.Locale,
                    Seed = options.Seed,
                    OutputGain = 1.0,
                };
                return EspeakNgVocalEngine.SynthesizeToSamples(word, espeakOptions);
            }
            catch (InvalidOperationException)
            {
                // fall through
            }
        }

        var prosody = ProsodySpeechRenderer.RenderStem(word, options.Seed);
        if (prosody.Length > 0 && VocalStemNormalizer.Peak(prosody) > 1e-6)
            return prosody;

        throw new InvalidOperationException($"Composite engine could not synthesize '{word}'.");
    }
}
