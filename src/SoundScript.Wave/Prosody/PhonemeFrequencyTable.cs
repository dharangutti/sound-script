// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave.Prosody;

/// <summary>One phoneme's allowed pitch band and rhythmic length.</summary>
public readonly record struct PhonemeFrequencyRange(
    double MinHz,
    double MaxHz,
    double DurationBeats);

/// <summary>
/// The small, fixed phoneme → frequency-range table for the v3 prosody proof
/// of concept. Pure data, total mapping (unknown phonemes fall back to
/// <see cref="Default"/>), no randomness here — tone variation inside a range
/// is applied by ProsodyToneGenerator via the shared seeded PRNG.
///
/// Explicitly v3-scope, NOT production TTS: no formant synthesis, no
/// multi-language phoneme sets, no linguistic modelling — just enough of a
/// mapping to demonstrate "same word + same seed = same tone, different seed
/// = different but deterministic tone."
///
/// All bands sit inside a conversational F0 region (~160-330 Hz, roughly
/// E3-E4): vowels get the longest durations and widest bands (they carry the
/// intonation), nasals/liquids sit mid-band, plosives are short and low,
/// fricatives short and high — a crude but audible echo of speech prosody.
/// </summary>
public static class PhonemeFrequencyTable
{
    /// <summary>Fallback for phonemes without an explicit row.</summary>
    public static readonly PhonemeFrequencyRange Default = new(200.0, 240.0, 0.25);

    private static readonly Dictionary<string, PhonemeFrequencyRange> Table = new(StringComparer.Ordinal)
    {
        // vowels — long, wide bands
        ["aa"] = new(200.0, 280.0, 0.5),
        ["ee"] = new(240.0, 330.0, 0.5),
        ["oo"] = new(160.0, 220.0, 0.5),
        ["ai"] = new(220.0, 310.0, 0.5),
        ["au"] = new(180.0, 250.0, 0.5),

        // nasals and glides — mid band, medium length
        ["m"] = new(170.0, 210.0, 0.375),
        ["n"] = new(180.0, 220.0, 0.375),
        ["ng"] = new(170.0, 215.0, 0.375),
        ["w"] = new(175.0, 225.0, 0.25),

        // plosives — short, low
        ["p"] = new(160.0, 190.0, 0.125),
        ["b"] = new(160.0, 195.0, 0.125),
        ["t"] = new(175.0, 205.0, 0.125),
        ["d"] = new(170.0, 200.0, 0.125),
        ["k"] = new(180.0, 210.0, 0.125),
        ["g"] = new(175.0, 205.0, 0.125),
        ["ch"] = new(185.0, 215.0, 0.125),

        // fricatives — short, high
        ["s"] = new(250.0, 310.0, 0.25),
        ["sh"] = new(240.0, 300.0, 0.25),
        ["th"] = new(230.0, 285.0, 0.25),
        ["f"] = new(235.0, 290.0, 0.25),
        ["v"] = new(225.0, 280.0, 0.25),
        ["z"] = new(245.0, 305.0, 0.25),
        ["h"] = new(220.0, 270.0, 0.25),

        // liquids and affricate-like onsets — mid-high
        ["r"] = new(200.0, 260.0, 0.25),
        ["l"] = new(195.0, 255.0, 0.25),
        ["j"] = new(210.0, 270.0, 0.25),
    };

    /// <summary>Total lookup — unknown symbols get <see cref="Default"/>.</summary>
    public static PhonemeFrequencyRange Lookup(string phoneme) =>
        Table.TryGetValue(phoneme, out var range) ? range : Default;
}
