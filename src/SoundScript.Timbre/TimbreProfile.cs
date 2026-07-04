namespace SoundScript.Timbre;

/// <summary>
/// Declarative timbre attributes for a single phoneme. Pure data — every
/// property has a deterministic default so synthesis never branches on null.
/// </summary>
public sealed class TimbreProfile
{
    /// <summary>Plosive/fricative burst length at note onset.</summary>
    public double BurstMs { get; init; } = 0;

    /// <summary>Noise layer mix (0 = pure voicing, 1 = pure noise).</summary>
    public double Noise { get; init; } = 0.1;

    /// <summary>High-frequency emphasis (0 = dark, 1 = bright).</summary>
    public double Brightness { get; init; } = 0.5;

    /// <summary>First formant centre frequency in Hz.</summary>
    public double Formant1Hz { get; init; } = 500;

    /// <summary>Second formant centre frequency in Hz.</summary>
    public double Formant2Hz { get; init; } = 1500;

    /// <summary>Third formant centre frequency in Hz.</summary>
    public double Formant3Hz { get; init; } = 2500;

    /// <summary>First formant bandwidth in Hz.</summary>
    public double Formant1BwHz { get; init; } = 80;

    /// <summary>Second formant bandwidth in Hz.</summary>
    public double Formant2BwHz { get; init; } = 110;

    /// <summary>Third formant bandwidth in Hz.</summary>
    public double Formant3BwHz { get; init; } = 150;

    /// <summary>Vowel transition smoothing (0 = abrupt, 1 = smooth).</summary>
    public double Smoothness { get; init; } = 0.5;

    /// <summary>Nasal resonance amount (0 = oral, 1 = nasal).</summary>
    public double Nasal { get; init; } = 0;

    /// <summary>Vowel openness (0 = closed, 1 = open).</summary>
    public double Openness { get; init; } = 0.5;

    /// <summary>Creates a copy with selective overrides.</summary>
    public TimbreProfile With(
        double? burstMs = null,
        double? noise = null,
        double? brightness = null,
        double? formant1Hz = null,
        double? formant2Hz = null,
        double? formant3Hz = null,
        double? formant1BwHz = null,
        double? formant2BwHz = null,
        double? formant3BwHz = null,
        double? smoothness = null,
        double? nasal = null,
        double? openness = null) =>
        new()
        {
            BurstMs = burstMs ?? BurstMs,
            Noise = noise ?? Noise,
            Brightness = brightness ?? Brightness,
            Formant1Hz = formant1Hz ?? Formant1Hz,
            Formant2Hz = formant2Hz ?? Formant2Hz,
            Formant3Hz = formant3Hz ?? Formant3Hz,
            Formant1BwHz = formant1BwHz ?? Formant1BwHz,
            Formant2BwHz = formant2BwHz ?? Formant2BwHz,
            Formant3BwHz = formant3BwHz ?? Formant3BwHz,
            Smoothness = smoothness ?? Smoothness,
            Nasal = nasal ?? Nasal,
            Openness = openness ?? Openness
        };

    /// <summary>Default timbre used when no phoneme-specific profile exists.</summary>
    public static TimbreProfile Default { get; } = new();

    /// <summary>Applies non-null override fields onto a baseline profile.</summary>
    public static TimbreProfile ApplyOverrides(TimbreProfile baseline, TimbreProfileOverrides overrides) =>
        baseline.With(
            burstMs: overrides.BurstMs,
            noise: overrides.Noise,
            brightness: overrides.Brightness,
            formant1Hz: overrides.Formant1Hz,
            formant2Hz: overrides.Formant2Hz,
            formant3Hz: overrides.Formant3Hz,
            formant1BwHz: overrides.Formant1BwHz,
            formant2BwHz: overrides.Formant2BwHz,
            formant3BwHz: overrides.Formant3BwHz,
            smoothness: overrides.Smoothness,
            nasal: overrides.Nasal,
            openness: overrides.Openness);
}

/// <summary>Partial timbre overrides parsed from SoundCSS (only set properties apply).</summary>
public sealed class TimbreProfileOverrides
{
    public double? BurstMs { get; init; }
    public double? Noise { get; init; }
    public double? Brightness { get; init; }
    public double? Formant1Hz { get; init; }
    public double? Formant2Hz { get; init; }
    public double? Formant3Hz { get; init; }
    public double? Formant1BwHz { get; init; }
    public double? Formant2BwHz { get; init; }
    public double? Formant3BwHz { get; init; }
    public double? Smoothness { get; init; }
    public double? Nasal { get; init; }
    public double? Openness { get; init; }
}
