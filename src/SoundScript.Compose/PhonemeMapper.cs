using SoundScript.Core.Notation;

namespace SoundScript.Compose;

/// <summary>The five gesture archetypes a phoneme can map to.</summary>
public enum GestureKind
{
    Staccato,
    Legato,
    Accent,
    Swell,
    Fade
}

/// <summary>
/// One musical gesture: pure data describing how a single phoneme sounds.
/// Kind selects the shaping archetype, pitch/octave the note, duration the
/// rhythmic value (e = eighth, q = quarter — the existing notation durations).
/// </summary>
public readonly record struct MusicalGesture(
    GestureKind Kind,
    PitchClass Pitch,
    int Octave,
    NoteDuration Duration);

/// <summary>
/// Deterministic phoneme → gesture table. Pure data, no randomness, no
/// platform-dependent behaviour; extend by adding rows to <see cref="Table"/>.
/// Unknown phonemes fall back to <see cref="DefaultGesture"/> so the mapping
/// is total.
/// </summary>
public static class PhonemeMapper
{
    /// <summary>Fallback for phonemes without an explicit row.</summary>
    public static readonly MusicalGesture DefaultGesture =
        new(GestureKind.Legato, PitchClass.C, 4, NoteDuration.Eighth);

    // Every phoneme sits within a perfect fifth — A3 to E4 (7 semitones) — instead
    // of spanning multiple octaves. Two things make this the right width, not just
    // "narrower": (1) natural speech F0 moves in small steps around a baseline
    // pitch, so a wide-ranging table (the original spanned C3-D5, over two octaves)
    // reads as an arpeggiated tune rather than spoken prosody; (2) the shared
    // Interpreter step MelodicContour (SoundScript.Midi/MelodicContour.cs) octave-
    // shifts any note-to-note leap over 7 semitones to smooth "wide" melodic jumps —
    // useful for hand-written music, but it would silently re-widen a phoneme
    // table that still had gaps over a fifth, undoing the narrowing here. Keeping
    // every entry within A3-E4 guarantees no pair of phonemes can trigger it.
    private static readonly Dictionary<string, MusicalGesture> Table = new(StringComparer.Ordinal)
    {
        // plosives → staccato
        ["p"] = new(GestureKind.Staccato, PitchClass.A, 3, NoteDuration.Eighth),
        ["t"] = new(GestureKind.Staccato, PitchClass.B, 3, NoteDuration.Eighth),
        ["k"] = new(GestureKind.Staccato, PitchClass.B, 3, NoteDuration.Eighth),
        ["b"] = new(GestureKind.Staccato, PitchClass.A, 3, NoteDuration.Eighth),
        ["d"] = new(GestureKind.Staccato, PitchClass.B, 3, NoteDuration.Eighth),
        ["g"] = new(GestureKind.Staccato, PitchClass.B, 3, NoteDuration.Eighth),
        ["ch"] = new(GestureKind.Staccato, PitchClass.B, 3, NoteDuration.Eighth),

        // nasals and glides → swell
        ["m"] = new(GestureKind.Swell, PitchClass.C, 4, NoteDuration.Quarter),
        ["n"] = new(GestureKind.Swell, PitchClass.C, 4, NoteDuration.Quarter),
        ["ng"] = new(GestureKind.Swell, PitchClass.C, 4, NoteDuration.Quarter),
        ["w"] = new(GestureKind.Swell, PitchClass.C, 4, NoteDuration.Eighth),

        // fricatives → fade
        ["s"] = new(GestureKind.Fade, PitchClass.D, 4, NoteDuration.Eighth),
        ["sh"] = new(GestureKind.Fade, PitchClass.D, 4, NoteDuration.Eighth),
        ["th"] = new(GestureKind.Fade, PitchClass.D, 4, NoteDuration.Eighth),
        ["f"] = new(GestureKind.Fade, PitchClass.C, 4, NoteDuration.Eighth),
        ["v"] = new(GestureKind.Fade, PitchClass.D, 4, NoteDuration.Eighth),
        ["z"] = new(GestureKind.Fade, PitchClass.D, 4, NoteDuration.Eighth),
        ["h"] = new(GestureKind.Fade, PitchClass.C, 4, NoteDuration.Eighth),

        // liquids and affricate-like onsets → accent
        ["r"] = new(GestureKind.Accent, PitchClass.D, 4, NoteDuration.Eighth),
        ["l"] = new(GestureKind.Accent, PitchClass.C, 4, NoteDuration.Eighth),
        ["j"] = new(GestureKind.Accent, PitchClass.D, 4, NoteDuration.Eighth),

        // vowels → legato
        ["aa"] = new(GestureKind.Legato, PitchClass.D, 4, NoteDuration.Quarter),
        ["ee"] = new(GestureKind.Legato, PitchClass.E, 4, NoteDuration.Quarter),
        ["oo"] = new(GestureKind.Legato, PitchClass.B, 3, NoteDuration.Quarter),
        ["ai"] = new(GestureKind.Legato, PitchClass.E, 4, NoteDuration.Quarter),
        ["au"] = new(GestureKind.Legato, PitchClass.C, 4, NoteDuration.Quarter),
    };

    /// <summary>Maps a phoneme symbol to its gesture, falling back to <see cref="DefaultGesture"/>.</summary>
    public static MusicalGesture Map(string phoneme) =>
        Table.TryGetValue(phoneme, out var gesture) ? gesture : DefaultGesture;

    /// <summary>Looks up a phoneme without applying the fallback.</summary>
    public static bool TryMap(string phoneme, out MusicalGesture gesture) =>
        Table.TryGetValue(phoneme, out gesture);
}
