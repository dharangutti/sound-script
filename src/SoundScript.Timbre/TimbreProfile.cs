namespace SoundScript.Timbre;

/// <summary>Harmonic amplitude rolloff shape applied above the fundamental (V4.1.1).</summary>
public enum HarmonicRolloffCurve
{
    /// <summary>Legacy brightness-tilt behavior (V4.1 default, unchanged).</summary>
    Default,
    Exponential,
    Linear,
    Polynomial
}

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

    /// <summary>Fundamental harmonic amplitude (cycle synthesis).</summary>
    public double Harmonic1 { get; init; } = 0.9;

    /// <summary>Second harmonic amplitude.</summary>
    public double Harmonic2 { get; init; } = 0.5;

    /// <summary>Third harmonic amplitude.</summary>
    public double Harmonic3 { get; init; } = 0.25;

    /// <summary>Fricative noise layer per cycle.</summary>
    public double NoiseFricative { get; init; } = 0.1;

    /// <summary>Plosive noise layer per cycle.</summary>
    public double NoisePlosive { get; init; } = 0.05;

    /// <summary>Consonant transient attack length in ms.</summary>
    public double TransientMs { get; init; } = 6;

    /// <summary>Harmonic amplitude rolloff shape above the fundamental (V4.1.1).</summary>
    public HarmonicRolloffCurve HarmonicRolloff { get; init; } = HarmonicRolloffCurve.Default;

    /// <summary>Formant Q multiplier — higher narrows bandwidth (sharper vowels), lower widens it (V4.1.1).</summary>
    public double FormantQ { get; init; } = 1.0;

    /// <summary>Fricative noise band-pass centre frequency in Hz; 0 = engine default (V4.1.1).</summary>
    public double NoiseBandHz { get; init; } = 0;

    /// <summary>Frame-to-frame parameter smoothing hint, 0 = snap, 1 = max smoothing (V4.1.1).</summary>
    public double FrameSmoothing { get; init; } = 0.2;

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
        double? openness = null,
        double? harmonic1 = null,
        double? harmonic2 = null,
        double? harmonic3 = null,
        double? noiseFricative = null,
        double? noisePlosive = null,
        double? transientMs = null,
        HarmonicRolloffCurve? harmonicRolloff = null,
        double? formantQ = null,
        double? noiseBandHz = null,
        double? frameSmoothing = null) =>
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
            Openness = openness ?? Openness,
            Harmonic1 = harmonic1 ?? Harmonic1,
            Harmonic2 = harmonic2 ?? Harmonic2,
            Harmonic3 = harmonic3 ?? Harmonic3,
            NoiseFricative = noiseFricative ?? NoiseFricative,
            NoisePlosive = noisePlosive ?? NoisePlosive,
            TransientMs = transientMs ?? TransientMs,
            HarmonicRolloff = harmonicRolloff ?? HarmonicRolloff,
            FormantQ = formantQ ?? FormantQ,
            NoiseBandHz = noiseBandHz ?? NoiseBandHz,
            FrameSmoothing = frameSmoothing ?? FrameSmoothing
        };

    /// <summary>Linearly interpolates numeric fields between two profiles (frame continuity, V4.1.1).</summary>
    public static TimbreProfile Lerp(TimbreProfile from, TimbreProfile to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new TimbreProfile
        {
            BurstMs = Lerp(from.BurstMs, to.BurstMs, t),
            Noise = Lerp(from.Noise, to.Noise, t),
            Brightness = Lerp(from.Brightness, to.Brightness, t),
            Formant1Hz = Lerp(from.Formant1Hz, to.Formant1Hz, t),
            Formant2Hz = Lerp(from.Formant2Hz, to.Formant2Hz, t),
            Formant3Hz = Lerp(from.Formant3Hz, to.Formant3Hz, t),
            Formant1BwHz = Lerp(from.Formant1BwHz, to.Formant1BwHz, t),
            Formant2BwHz = Lerp(from.Formant2BwHz, to.Formant2BwHz, t),
            Formant3BwHz = Lerp(from.Formant3BwHz, to.Formant3BwHz, t),
            Smoothness = to.Smoothness,
            Nasal = Lerp(from.Nasal, to.Nasal, t),
            Openness = Lerp(from.Openness, to.Openness, t),
            Harmonic1 = Lerp(from.Harmonic1, to.Harmonic1, t),
            Harmonic2 = Lerp(from.Harmonic2, to.Harmonic2, t),
            Harmonic3 = Lerp(from.Harmonic3, to.Harmonic3, t),
            NoiseFricative = Lerp(from.NoiseFricative, to.NoiseFricative, t),
            NoisePlosive = Lerp(from.NoisePlosive, to.NoisePlosive, t),
            TransientMs = to.TransientMs,
            HarmonicRolloff = to.HarmonicRolloff,
            FormantQ = Lerp(from.FormantQ, to.FormantQ, t),
            NoiseBandHz = Lerp(from.NoiseBandHz, to.NoiseBandHz, t),
            FrameSmoothing = to.FrameSmoothing
        };
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

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
            openness: overrides.Openness,
            harmonic1: overrides.Harmonic1,
            harmonic2: overrides.Harmonic2,
            harmonic3: overrides.Harmonic3,
            noiseFricative: overrides.NoiseFricative,
            noisePlosive: overrides.NoisePlosive,
            transientMs: overrides.TransientMs,
            harmonicRolloff: overrides.HarmonicRolloff,
            formantQ: overrides.FormantQ,
            noiseBandHz: overrides.NoiseBandHz,
            frameSmoothing: overrides.FrameSmoothing);
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
    public double? Harmonic1 { get; init; }
    public double? Harmonic2 { get; init; }
    public double? Harmonic3 { get; init; }
    public double? NoiseFricative { get; init; }
    public double? NoisePlosive { get; init; }
    public double? TransientMs { get; init; }
    public HarmonicRolloffCurve? HarmonicRolloff { get; init; }
    public double? FormantQ { get; init; }
    public double? NoiseBandHz { get; init; }
    public double? FrameSmoothing { get; init; }
}
