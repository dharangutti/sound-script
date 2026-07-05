namespace SoundScript.Prosody;

/// <summary>
/// Computes the micro-pitch offset each syllable adds on top of its word's
/// resolved pitch (base + phrase delta). Stress-sensitive and small — every
/// offset is well within the required ±3 semitone bound.
/// </summary>
public static class SyllableContourGenerator
{
    private const int PrimaryOffset = 2;
    private const int SecondaryOffset = 1;
    private const int UnstressedOffset = 0;

    /// <summary>Computes one semitone offset per syllable, from its stress level.</summary>
    public static IReadOnlyList<int> GenerateOffsets(IReadOnlyList<StressLevel> stress)
    {
        var offsets = new int[stress.Count];
        for (var i = 0; i < stress.Count; i++)
        {
            offsets[i] = stress[i] switch
            {
                StressLevel.Primary => PrimaryOffset,
                StressLevel.Secondary => SecondaryOffset,
                _ => UnstressedOffset,
            };
        }

        return offsets;
    }
}
