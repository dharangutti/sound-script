using SoundScript.Wordbank;

namespace SoundScript.Prosody;

/// <summary>
/// Safety-net pass over a flat per-syllable MIDI sequence: a single
/// left-to-right walk that pulls any adjacent jump back to at most
/// <c>maxAdjacentJump</c> semitones and keeps the running phrase range at
/// most <c>maxPhraseRange</c> semitones. Deterministic and pure. This is a
/// backstop, not the primary shaping mechanism — <see cref="WordPitchTable"/>,
/// <see cref="PhraseContourEngine"/>, and <see cref="SyllableContourGenerator"/>
/// combine to a theoretical worst case of about 13 semitones, so the range
/// bound only needs to catch pathological inputs, not the everyday
/// word/phrase/syllable contour.
/// </summary>
public static class ProsodyClamp
{
    private static readonly Wordbank.Models.ClampSettings Settings = WordbankCatalog.Default.WordProsody.Clamp;

    /// <summary>Clamps a per-syllable MIDI sequence to the bounds described above.</summary>
    public static IReadOnlyList<int> Clamp(IReadOnlyList<int> midiSequence)
    {
        if (midiSequence.Count == 0)
            return midiSequence;

        var result = new int[midiSequence.Count];
        result[0] = midiSequence[0];
        var min = result[0];
        var max = result[0];

        for (var i = 1; i < midiSequence.Count; i++)
        {
            var target = midiSequence[i];

            var diff = target - result[i - 1];
            if (diff > Settings.MaxAdjacentJump)
                target = result[i - 1] + Settings.MaxAdjacentJump;
            else if (diff < -Settings.MaxAdjacentJump)
                target = result[i - 1] - Settings.MaxAdjacentJump;

            if (target - min > Settings.MaxPhraseRange)
                target = min + Settings.MaxPhraseRange;
            else if (max - target > Settings.MaxPhraseRange)
                target = max - Settings.MaxPhraseRange;

            result[i] = target;
            min = Math.Min(min, target);
            max = Math.Max(max, target);
        }

        return result;
    }
}
