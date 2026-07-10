using SoundScript.Wordbank;
using SoundScript.Wordbank.Models;

namespace SoundScript.Compose;

/// <summary>
/// Deterministic phoneme → gesture table. Pure data, no randomness, no
/// platform-dependent behaviour; extend by adding rows to the wordbank locale pack.
/// Unknown phonemes fall back to <see cref="DefaultGesture"/> so the mapping
/// is total.
/// </summary>
public static class PhonemeMapper
{
    private static LocalePack Locale => WordbankCatalog.Active;
    /// <summary>Fallback for phonemes without an explicit row.</summary>
    public static MusicalGesture DefaultGesture =>
        ToGesture(WordbankCatalog.Active.PhonemeCompose.DefaultGesture);
    private static Dictionary<string, MusicalGesture> Table => BuildTable();

    /// <summary>Maps a phoneme symbol to its gesture, falling back to <see cref="DefaultGesture"/>.</summary>
    public static MusicalGesture Map(string phoneme) =>
        Table.TryGetValue(phoneme, out var gesture) ? gesture : DefaultGesture;

    /// <summary>Looks up a phoneme without applying the fallback.</summary>
    public static bool TryMap(string phoneme, out MusicalGesture gesture) =>
        Table.TryGetValue(phoneme, out gesture);

    private static Dictionary<string, MusicalGesture> BuildTable()
    {
        var table = new Dictionary<string, MusicalGesture>(StringComparer.Ordinal);
        foreach (var (phoneme, gesture) in Locale.ComposeGestureMap)
            table[phoneme] = ToGesture(gesture);

        return table;
    }

    private static MusicalGesture ToGesture(PhonemeGesture gesture) =>
        new(
            WordbankMappings.ParseGestureKind(gesture.Kind),
            WordbankMappings.ParsePitch(gesture.Pitch),
            gesture.Octave,
            WordbankMappings.ParseDuration(gesture.Duration));
}
