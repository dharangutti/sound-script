// UNDER DEVELOPMENT — v3
using SoundScript.Wave.Synthesis;

namespace SoundScript.Wave.Prosody;

/// <summary>
/// One tone (or inter-word rest) in a generated prosody sequence, in
/// grammar-agnostic units: free-form Hz (not MIDI-quantized — exactly the
/// capability MIDI cannot express) and beats (converted to seconds by the
/// adapter through the shared tempo map).
/// </summary>
public readonly record struct ProsodyTone(
    double FrequencyHz,
    double DurationBeats,
    double Velocity,
    bool IsRest,
    PhonemeClass Class = PhonemeClass.Nasal)
{
    public static ProsodyTone Rest(double durationBeats) =>
        new(0.0, durationBeats, 0.0, IsRest: true);
}

/// <summary>
/// text → phoneme sequence → deterministic tone sequence, for the v3
/// <c>speak "..." voice=... seed=...</c> directive.
///
/// Each phoneme's pitch is drawn from its <see cref="PhonemeFrequencyTable"/>
/// band using the shared seeded PRNG (<see cref="DeterministicRandom"/> —
/// the same helper the humanize jitter uses): same text + same seed =
/// byte-identical tone sequence, forever, on every platform; a different
/// seed gives a different but equally deterministic "take." A null seed is
/// derived from the text itself (file content), never wall-clock.
/// </summary>
public static class ProsodyToneGenerator
{
    private const double WordGapBeats = 0.25;

    // Per-phoneme note velocity. Speech phonemes are short, transient blips
    // (57-227 ms, separated by rests) that must read as "voice" against the
    // continuous, sustained melody/harmony/bass bed a program can layer under
    // them — and in the very same ~130-900 Hz register, where they mask
    // easily. Because the mixdown's peak-normalization is a single global
    // down-scale shared by every track (see Mixing.Mixer — never a per-track
    // gain, never compression), the voice:instrumental loudness ratio is
    // invariant to that normalization and scales linearly with this constant.
    // It is deliberately near the [0,1] velocity ceiling (leaving a hair of
    // headroom) so speech has genuine presence in the exported WAV rather than
    // sitting ~4 dB under a busy backing arrangement; raising it changes only
    // the vocal track's own pre-mix loudness, not the mixdown policy.
    private const double ToneVelocity = 0.95;

    // Salt for pitch draws; distinct from the humanize salts so a shared seed
    // can never correlate prosody pitch with note jitter.
    private const int PitchSalt = 100;

    public static IReadOnlyList<ProsodyTone> Generate(string text, string voice, int? explicitSeed)
    {
        if (!voice.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"speak voice '{voice}' is not supported — v3 ships a single proof-of-concept " +
                "voice; use voice=default (or omit the parameter).");
        }

        var words = SplitWords(text);
        if (words.Count == 0)
            throw new InvalidOperationException("speak text must contain at least one letter.");

        // Explicit seed if given; otherwise derived from the text itself so
        // the same file always renders the same bytes (determinism safeguard).
        var seed = explicitSeed ?? DeterministicRandom.DeriveSeed(text.ToLowerInvariant());

        var tones = new List<ProsodyTone>();
        var phonemeIndex = 0;

        for (var w = 0; w < words.Count; w++)
        {
            if (w > 0)
                tones.Add(ProsodyTone.Rest(WordGapBeats));

            foreach (var phoneme in GraphemePhonemeSplitter.Split(words[w]))
            {
                var range = PhonemeFrequencyTable.Lookup(phoneme);
                var unit = DeterministicRandom.Unit01(seed, phonemeIndex, PitchSalt);
                var frequency = range.MinHz + unit * (range.MaxHz - range.MinHz);

                tones.Add(new ProsodyTone(frequency, range.DurationBeats, ToneVelocity, IsRest: false, range.Class));
                phonemeIndex++;
            }
        }

        return tones;
    }

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var isLetter = i < text.Length && char.IsLetter(text[i]);
            if (isLetter && start < 0)
            {
                start = i;
            }
            else if (!isLetter && start >= 0)
            {
                words.Add(text[start..i]);
                start = -1;
            }
        }

        return words;
    }
}
