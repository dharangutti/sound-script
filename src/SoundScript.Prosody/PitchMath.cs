using SoundScript.Core.Notation;

namespace SoundScript.Prosody;

/// <summary>
/// Converts an absolute MIDI number into notated pitch (pitch class,
/// accidental, octave). Prosody pitch targets are computed in semitones, so
/// unlike the natural-only <c>PhonemeMapper</c> table, results may land on a
/// black key — this always spells those chromatically as a sharp (C#, D#,
/// F#, G#, A#), a fixed, deterministic convention.
/// </summary>
internal static class PitchMath
{
    private static readonly (PitchClass PitchClass, AccidentalType Accidental)[] ChromaticSpelling =
    [
        (PitchClass.C, AccidentalType.None),  // 0
        (PitchClass.C, AccidentalType.Sharp), // 1
        (PitchClass.D, AccidentalType.None),  // 2
        (PitchClass.D, AccidentalType.Sharp), // 3
        (PitchClass.E, AccidentalType.None),  // 4
        (PitchClass.F, AccidentalType.None),  // 5
        (PitchClass.F, AccidentalType.Sharp), // 6
        (PitchClass.G, AccidentalType.None),  // 7
        (PitchClass.G, AccidentalType.Sharp), // 8
        (PitchClass.A, AccidentalType.None),  // 9
        (PitchClass.A, AccidentalType.Sharp), // 10
        (PitchClass.B, AccidentalType.None),  // 11
    ];

    /// <summary>Resolves a MIDI number into pitch class, accidental, and octave.</summary>
    internal static (PitchClass PitchClass, AccidentalType Accidental, int Octave) FromMidiNumber(int midi)
    {
        var pitchClassValue = ((midi % 12) + 12) % 12;
        var octave = (midi - pitchClassValue) / 12 - 1;
        var (pitchClass, accidental) = ChromaticSpelling[pitchClassValue];
        return (pitchClass, accidental, octave);
    }
}
