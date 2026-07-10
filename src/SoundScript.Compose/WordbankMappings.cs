namespace SoundScript.Compose;

internal static class WordbankMappings
{
    internal static GestureKind ParseGestureKind(string kind) => kind switch
    {
        "staccato" => GestureKind.Staccato,
        "legato" => GestureKind.Legato,
        "accent" => GestureKind.Accent,
        "swell" => GestureKind.Swell,
        "fade" => GestureKind.Fade,
        _ => GestureKind.Legato,
    };

    internal static Core.Notation.PitchClass ParsePitch(string pitch) => pitch switch
    {
        "C" => Core.Notation.PitchClass.C,
        "D" => Core.Notation.PitchClass.D,
        "E" => Core.Notation.PitchClass.E,
        "F" => Core.Notation.PitchClass.F,
        "G" => Core.Notation.PitchClass.G,
        "A" => Core.Notation.PitchClass.A,
        "B" => Core.Notation.PitchClass.B,
        _ => Core.Notation.PitchClass.C,
    };

    internal static Core.Notation.NoteDuration ParseDuration(string duration) => duration switch
    {
        "whole" => Core.Notation.NoteDuration.Whole,
        "half" => Core.Notation.NoteDuration.Half,
        "quarter" => Core.Notation.NoteDuration.Quarter,
        "eighth" => Core.Notation.NoteDuration.Eighth,
        "sixteenth" => Core.Notation.NoteDuration.Eighth,
        _ => Core.Notation.NoteDuration.Eighth,
    };
}
