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
