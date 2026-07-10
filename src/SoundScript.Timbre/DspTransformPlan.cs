namespace SoundScript.Timbre;

/// <summary>
/// Canonical voice metadata used to compute pitch/formant transforms relative to
/// a specific recording. <see cref="BasePitchHz"/> comes from the WordbankNormalizer
/// sidecar (Prompts 1–2); formant frequencies default to the neutral
/// <see cref="TimbreProfile.Default"/> when a per-voice estimate is unavailable.
/// </summary>
public sealed record CanonicalVoiceMetadata(
    double BasePitchHz,
    double Formant1Hz,
    double Formant2Hz,
    double Formant3Hz)
{
    /// <summary>Neutral reference voice (~120 Hz male-ish base, default formants).</summary>
    public static CanonicalVoiceMetadata Default { get; } = new(
        BasePitchHz: 120.0,
        Formant1Hz: TimbreProfile.Default.Formant1Hz,
        Formant2Hz: TimbreProfile.Default.Formant2Hz,
        Formant3Hz: TimbreProfile.Default.Formant3Hz);
}

/// <summary>Shape of a single EQ contribution.</summary>
public enum EqShelf { LowShelf, HighShelf, Peak }

/// <summary>One EQ band: a gain (dB) applied around <see cref="PivotHz"/>.</summary>
public sealed record EqBand(double PivotHz, double GainDb, EqShelf Shelf = EqShelf.Peak);

/// <summary>Periodic pitch modulation.</summary>
public sealed record VibratoParams(double RateHz, double DepthSemitones)
{
    public static VibratoParams None { get; } = new(0.0, 0.0);

    /// <summary>True when this vibrato actually modulates the signal.</summary>
    public bool IsActive => RateHz > 0 && DepthSemitones > 0;
}

/// <summary>
/// Concrete, deterministic DSP transform derived from a
/// <see cref="SoundCssPronunciation"/> and <see cref="CanonicalVoiceMetadata"/>.
/// Consumed by the DSP rendering layer. All fields are absolute/normalized so the
/// plan is self-contained and reproducible.
/// </summary>
public sealed record DspTransformPlan
{
    /// <summary>Pitch offset in semitones (relative to the canonical base, clamped to a human band).</summary>
    public double PitchSemitones { get; init; }

    /// <summary>Duration multiplier (1.0 = unchanged, &gt;1 slower/longer, &lt;1 faster/shorter).</summary>
    public double TimeStretch { get; init; } = 1.0;

    /// <summary>Overall gain in dB.</summary>
    public double GainDb { get; init; }

    /// <summary>EQ bands applied in listed order.</summary>
    public IReadOnlyList<EqBand> EqBands { get; init; } = [];

    /// <summary>Formant frequency scaling ratio (1.0 = unchanged).</summary>
    public double FormantShift { get; init; } = 1.0;

    /// <summary>Vibrato modulation parameters.</summary>
    public VibratoParams Vibrato { get; init; } = VibratoParams.None;

    /// <summary>Additive breath/noise layer amount (0..1).</summary>
    public double NoiseLayer { get; init; }

    /// <summary>Absolute target fundamental in Hz (base pitch shifted by <see cref="PitchSemitones"/>).</summary>
    public double TargetPitchHz { get; init; }
}
