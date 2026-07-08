// UNDER DEVELOPMENT — v3
namespace SoundScript.Wave.Prosody;

/// <summary>
/// The synthesis family a phoneme belongs to — drives which oscillator
/// <see cref="ProsodyToneGenerator"/>/<c>AstToNoteEventAdapter.EmitSpeech</c>
/// uses and how its <see cref="PhonemeFrequencyRange"/> is interpreted (pitch
/// band for tonal classes, filter cutoff band for noise classes).
/// </summary>
public enum PhonemeClass
{
    Vowel,
    Nasal,
    Liquid,
    Plosive,
    Fricative
}

/// <summary>
/// One phoneme's allowed frequency band and rhythmic length. For
/// <see cref="PhonemeClass.Vowel"/>, <see cref="PhonemeClass.Nasal"/>, and
/// <see cref="PhonemeClass.Liquid"/> the band is a pitch (oscillator
/// frequency); for <see cref="PhonemeClass.Plosive"/> and
/// <see cref="PhonemeClass.Fricative"/> it is a noise low-pass cutoff band —
/// see the class doc comment below.
/// </summary>
public readonly record struct PhonemeFrequencyRange(
    double MinHz,
    double MaxHz,
    double DurationBeats,
    PhonemeClass Class);

/// <summary>
/// The small, fixed phoneme → frequency-range table for the v3 prosody proof
/// of concept. Pure data, total mapping (unknown phonemes fall back to
/// <see cref="Default"/>), no randomness here — tone variation inside a range
/// is applied by ProsodyToneGenerator via the shared seeded PRNG.
///
/// v7 scope: not production TTS or a multi-formant filter bank, no
/// multi-language phoneme sets, no linguistic modelling — but each
/// <see cref="PhonemeClass"/> now gets its own deterministic timbre rather
/// than a single flat sine tone (see AstToNoteEventAdapter.EmitSpeech):
/// vowels stack a soft formant-ish overtone on the fundamental, nasals/
/// liquids stay a plain tone, and plosives/fricatives synthesize from
/// deterministic filtered noise instead of a tone. For the noise classes,
/// <see cref="PhonemeFrequencyRange.MinHz"/>/<see cref="PhonemeFrequencyRange.MaxHz"/>
/// describe the low-pass cutoff band shaping the noise burst/hiss, not a
/// pitch — a plosive's "160-190 Hz" would sound like a dull thump, so those
/// rows sit much higher (short bursts ~1200-2000 Hz, sustained hiss
/// ~3000-6000 Hz) than the tonal classes' conversational F0 region
/// (~160-330 Hz, roughly E3-E4).
/// </summary>
public static class PhonemeFrequencyTable
{
    /// <summary>Fallback for phonemes without an explicit row.</summary>
    public static readonly PhonemeFrequencyRange Default = new(200.0, 240.0, 0.25, PhonemeClass.Nasal);

    private static readonly Dictionary<string, PhonemeFrequencyRange> Table = new(StringComparer.Ordinal)
    {
        // vowels — long, wide pitch bands (they carry the intonation)
        ["aa"] = new(200.0, 280.0, 0.5, PhonemeClass.Vowel),
        ["ee"] = new(240.0, 330.0, 0.5, PhonemeClass.Vowel),
        ["oo"] = new(160.0, 220.0, 0.5, PhonemeClass.Vowel),
        ["ai"] = new(220.0, 310.0, 0.5, PhonemeClass.Vowel),
        ["au"] = new(180.0, 250.0, 0.5, PhonemeClass.Vowel),

        // nasals and glides — mid pitch band, medium length
        ["m"] = new(170.0, 210.0, 0.375, PhonemeClass.Nasal),
        ["n"] = new(180.0, 220.0, 0.375, PhonemeClass.Nasal),
        ["ng"] = new(170.0, 215.0, 0.375, PhonemeClass.Nasal),
        ["w"] = new(175.0, 225.0, 0.25, PhonemeClass.Nasal),

        // plosives — short filtered-noise burst, low cutoff band
        ["p"] = new(1200.0, 1500.0, 0.125, PhonemeClass.Plosive),
        ["b"] = new(1300.0, 1600.0, 0.125, PhonemeClass.Plosive),
        ["t"] = new(1500.0, 1800.0, 0.125, PhonemeClass.Plosive),
        ["d"] = new(1400.0, 1700.0, 0.125, PhonemeClass.Plosive),
        ["k"] = new(1700.0, 2000.0, 0.125, PhonemeClass.Plosive),
        ["g"] = new(1600.0, 1900.0, 0.125, PhonemeClass.Plosive),
        ["ch"] = new(1800.0, 2000.0, 0.125, PhonemeClass.Plosive),

        // fricatives — sustained filtered-noise hiss, high cutoff band
        ["s"] = new(4500.0, 6000.0, 0.25, PhonemeClass.Fricative),
        ["sh"] = new(3500.0, 5000.0, 0.25, PhonemeClass.Fricative),
        ["th"] = new(3800.0, 5200.0, 0.25, PhonemeClass.Fricative),
        ["f"] = new(3000.0, 4200.0, 0.25, PhonemeClass.Fricative),
        ["v"] = new(3000.0, 4200.0, 0.25, PhonemeClass.Fricative),
        ["z"] = new(4500.0, 6000.0, 0.25, PhonemeClass.Fricative),
        ["h"] = new(3200.0, 4500.0, 0.25, PhonemeClass.Fricative),

        // liquids and affricate-like onsets — mid-high pitch band
        ["r"] = new(200.0, 260.0, 0.25, PhonemeClass.Liquid),
        ["l"] = new(195.0, 255.0, 0.25, PhonemeClass.Liquid),
        ["j"] = new(210.0, 270.0, 0.25, PhonemeClass.Liquid),
    };

    /// <summary>Total lookup — unknown symbols get <see cref="Default"/>.</summary>
    public static PhonemeFrequencyRange Lookup(string phoneme) =>
        Table.TryGetValue(phoneme, out var range) ? range : Default;
}
