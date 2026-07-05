namespace SoundScript.Prosody;

/// <summary>
/// Declarative base-pitch table: word category + phrase position → semitone
/// offset from C4 (MIDI 60). Pure data, no randomness — extend by editing the
/// rows below.
/// </summary>
public static class WordPitchTable
{
    /// <summary>MIDI number of the table's centre pitch, C4.</summary>
    public const int CenterMidi = 60;

    private static readonly Dictionary<(WordCategory, PhrasePosition), int> Offsets = new()
    {
        [(WordCategory.Content, PhrasePosition.Start)] = 4,
        [(WordCategory.Content, PhrasePosition.Middle)] = 0,
        [(WordCategory.Content, PhrasePosition.End)] = -3,
        [(WordCategory.Function, PhrasePosition.Start)] = -7,
        [(WordCategory.Function, PhrasePosition.Middle)] = -7,
        [(WordCategory.Function, PhrasePosition.End)] = -7,
    };

    /// <summary>Base MIDI number for a word of the given category and phrase position.</summary>
    public static int BaseMidi(WordCategory category, PhrasePosition position) =>
        CenterMidi + Offsets[(category, position)];
}
