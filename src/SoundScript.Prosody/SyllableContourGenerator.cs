using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Computes the micro-pitch offset each syllable adds on top of its word's
/// resolved pitch (base + phrase delta). Stress-sensitive and small — every
/// offset is well within the required ±3 semitone bound.
/// </summary>
public static class SyllableContourGenerator
{
    private static Dictionary<string, int> Offsets => WordbankCatalog.Active.WordProsody.SyllableStressOffsets;

    /// <summary>Computes one semitone offset per syllable, from its stress level.</summary>
    public static IReadOnlyList<int> GenerateOffsets(IReadOnlyList<StressLevel> stress)
    {
        var result = new int[stress.Count];
        for (var i = 0; i < stress.Count; i++)
        {
            result[i] = stress[i] switch
            {
                StressLevel.Primary => Offsets["primary"],
                StressLevel.Secondary => Offsets["secondary"],
                _ => Offsets["unstressed"],
            };
        }

        return result;
    }
}
