// UNDER DEVELOPMENT — v3
using SoundScript.Wordbank;
using SoundScript.Wordbank.Models;

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
/// <see cref="PhonemeClass.Fricative"/> it is a noise low-pass cutoff band.
/// </summary>
public readonly record struct PhonemeFrequencyRange(
    double MinHz,
    double MaxHz,
    double DurationBeats,
    PhonemeClass Class);

/// <summary>
/// Phoneme → frequency-range table loaded from the wordbank locale pack.
/// Unknown phonemes fall back to <see cref="Default"/>.
/// </summary>
public static class PhonemeFrequencyTable
{
    private static readonly LocalePack Locale = WordbankCatalog.Default;

    /// <summary>Fallback for phonemes without an explicit row.</summary>
    public static readonly PhonemeFrequencyRange Default = ToRange(Locale.PhonemeWave.Default);

    private static readonly Dictionary<string, PhonemeFrequencyRange> Table = BuildTable();

    /// <summary>Total lookup — unknown symbols get <see cref="Default"/>.</summary>
    public static PhonemeFrequencyRange Lookup(string phoneme) =>
        Table.TryGetValue(phoneme, out var range) ? range : Default;

    private static Dictionary<string, PhonemeFrequencyRange> BuildTable()
    {
        var table = new Dictionary<string, PhonemeFrequencyRange>(StringComparer.Ordinal);
        foreach (var (phoneme, frequency) in Locale.WaveFrequencyMap)
            table[phoneme] = ToRange(frequency);

        return table;
    }

    private static PhonemeFrequencyRange ToRange(PhonemeFrequency frequency) =>
        new(frequency.MinHz, frequency.MaxHz, frequency.DurationBeats, ParseClass(frequency.Class));

    private static PhonemeClass ParseClass(string value) => value switch
    {
        "vowel" => PhonemeClass.Vowel,
        "nasal" => PhonemeClass.Nasal,
        "liquid" => PhonemeClass.Liquid,
        "plosive" => PhonemeClass.Plosive,
        "fricative" => PhonemeClass.Fricative,
        _ => PhonemeClass.Nasal,
    };
}
