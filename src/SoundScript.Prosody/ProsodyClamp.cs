namespace SoundScript.Prosody;

/// <summary>
/// Safety-net pass over a flat per-syllable MIDI sequence: a single
/// left-to-right walk that pulls any adjacent jump back to at most 5
/// semitones and keeps the running phrase range (max − min) at most 14
/// semitones. Deterministic and pure. This is a backstop, not the primary
/// shaping mechanism — <see cref="WordPitchTable"/>,
/// <see cref="PhraseContourEngine"/>, and <see cref="SyllableContourGenerator"/>
/// combine to a theoretical worst case of about 13 semitones, so the range
/// bound only needs to catch pathological inputs, not the everyday
/// word/phrase/syllable contour. It must stay well clear of the 7-semitone
/// range where the shared <see cref="SoundScript.Midi"/> Interpreter's
/// MelodicContour step starts octave-correcting leaps — which is guarded by
/// <see cref="MaxAdjacentJump"/>, not by this range bound, since the two
/// constraints are independent (adjacent-step size vs. whole-phrase span).
/// A range bound tight enough to equal MaxAdjacentJump would flatten the
/// entire point of planning pitch top-down (phrase → word → syllable)
/// instead of per fixed phoneme category, making prosody-composed melody
/// indistinguishable from the flat <see cref="SoundScript.Compose.PhonemeMapper"/>
/// table it exists to improve on.
/// </summary>
public static class ProsodyClamp
{
    private const int MaxAdjacentJump = 5;
    private const int MaxPhraseRange = 14;

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
            if (diff > MaxAdjacentJump)
                target = result[i - 1] + MaxAdjacentJump;
            else if (diff < -MaxAdjacentJump)
                target = result[i - 1] - MaxAdjacentJump;

            if (target - min > MaxPhraseRange)
                target = min + MaxPhraseRange;
            else if (max - target > MaxPhraseRange)
                target = max - MaxPhraseRange;

            result[i] = target;
            min = Math.Min(min, target);
            max = Math.Max(max, target);
        }

        return result;
    }
}
