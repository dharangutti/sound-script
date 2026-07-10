using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Declarative base-pitch table: word category + phrase position → semitone
/// offset from C4 (MIDI 60). Pure data, no randomness — values come from the
/// wordbank locale pack.
/// </summary>
public static class WordPitchTable
{
    private static Wordbank.Models.WordProsodyDocument Prosody => WordbankCatalog.Active.WordProsody;

    /// <summary>MIDI number of the table's centre pitch, C4.</summary>
    public static int CenterMidi => Prosody.CenterMidi;

    private static Dictionary<(WordCategory, PhrasePosition), int> Offsets => BuildOffsets();

    /// <summary>Base MIDI number for a word of the given category and phrase position.</summary>
    public static int BaseMidi(WordCategory category, PhrasePosition position) =>
        CenterMidi + Offsets[(category, position)];

    private static Dictionary<(WordCategory, PhrasePosition), int> BuildOffsets()
    {
        var content = Prosody.WordPitchOffsets["content"];
        var function = Prosody.WordPitchOffsets["function"];

        return new Dictionary<(WordCategory, PhrasePosition), int>
        {
            [(WordCategory.Content, PhrasePosition.Start)] = content.Start,
            [(WordCategory.Content, PhrasePosition.Middle)] = content.Middle,
            [(WordCategory.Content, PhrasePosition.End)] = content.End,
            [(WordCategory.Function, PhrasePosition.Start)] = function.Start,
            [(WordCategory.Function, PhrasePosition.Middle)] = function.Middle,
            [(WordCategory.Function, PhrasePosition.End)] = function.End,
        };
    }
}
