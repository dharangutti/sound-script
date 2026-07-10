namespace SoundScript.Timbre;

/// <summary>
/// Deterministic numeric mapping from a validated <see cref="SoundCssPronunciation"/>
/// (Prompt 3) plus <see cref="CanonicalVoiceMetadata"/> (Prompts 1–2) into a concrete
/// <see cref="DspTransformPlan"/>.
///
/// <para>Composition order is fixed: the <c>persona</c> preset is applied first as a
/// base, then explicit attributes compose on top (pitch/gain additive, speed/formant
/// multiplicative, EQ appended, vibrato/noise accumulated). Pure math — no randomness —
/// so the same inputs always yield the same plan.</para>
/// </summary>
public static class SoundCssDspMapper
{
    private const double MinPitchHz = 70.0;
    private const double MaxPitchHz = 500.0;
    private const double MinFormantHz = 250.0;
    private const double MaxFormantHz = 1200.0;

    /// <summary>Maps a pronunciation using the neutral reference voice.</summary>
    public static DspTransformPlan Map(SoundCssPronunciation pronunciation) =>
        Map(pronunciation, CanonicalVoiceMetadata.Default);

    /// <summary>Maps a pronunciation relative to a specific canonical voice.</summary>
    public static DspTransformPlan Map(SoundCssPronunciation pronunciation, CanonicalVoiceMetadata metadata)
    {
        var acc = new Accumulator();

        if (pronunciation.Persona is { } persona)
            ApplyPersona(acc, persona);

        if (pronunciation.Style is { } style)
            ApplyStyle(acc, style);
        if (pronunciation.Accent is { } accent)
            ApplyAccent(acc, accent);
        if (pronunciation.Speed is { } speed)
            acc.SpeedFactor *= SpeedFactor(speed);
        if (pronunciation.PitchSemitones is { } pitch)
            acc.PitchSemitones += pitch;
        if (pronunciation.Energy is { } energy)
            acc.GainDb += energy switch
            {
                SoundCssEnergy.High => 4.0,
                SoundCssEnergy.Low => -4.0,
                _ => 0.0,
            };
        if (pronunciation.Timbre is { } timbre)
            ApplyTimbre(acc, timbre);
        if (pronunciation.Gender is { } gender)
            ApplyGender(acc, gender);
        if (pronunciation.Age is { } age)
            ApplyAge(acc, age);
        if (pronunciation.Emotion is { } emotion)
            ApplyEmotion(acc, emotion);
        if (pronunciation.Breath is { } breath)
            ApplyBreath(acc, breath);
        if (pronunciation.Vibrato is { } vibrato)
            ApplyVibrato(acc, vibrato);

        return Finalize(acc, metadata);
    }

    /// <summary>Maps a persona preset on its own, relative to a canonical voice.</summary>
    public static DspTransformPlan MapPersona(SoundCssPersona persona, CanonicalVoiceMetadata metadata) =>
        Map(new SoundCssPronunciation { Persona = persona }, metadata);

    private static void ApplyPersona(Accumulator acc, SoundCssPersona persona)
    {
        switch (persona)
        {
            case SoundCssPersona.Narrator:
                acc.GainDb += 1.0;
                acc.SpeedFactor *= 0.95;
                acc.NoiseLayer += 0.05;
                acc.Eq.Add(new EqBand(300, +1.0, EqShelf.LowShelf));
                acc.Eq.Add(new EqBand(6000, -1.0, EqShelf.HighShelf));
                break;
            case SoundCssPersona.Robot:
                acc.NoiseLayer += 0.12;
                acc.FormantShift *= 1.0;
                acc.Eq.Add(new EqBand(1500, +3.0, EqShelf.Peak));
                acc.Eq.Add(new EqBand(400, -2.0, EqShelf.LowShelf));
                acc.Eq.Add(new EqBand(3000, +1.0, EqShelf.HighShelf));
                break;
            case SoundCssPersona.Soft:
                acc.GainDb += -3.0;
                acc.NoiseLayer += 0.2;
                acc.Eq.Add(new EqBand(4000, -2.0, EqShelf.HighShelf));
                acc.AddVibrato(5.0, 0.15);
                break;
            case SoundCssPersona.Bright:
                acc.GainDb += 1.0;
                acc.Eq.Add(new EqBand(4000, +5.0, EqShelf.HighShelf));
                acc.Eq.Add(new EqBand(2500, +2.0, EqShelf.Peak));
                break;
        }
    }

    private static void ApplyStyle(Accumulator acc, SoundCssStyle style)
    {
        switch (style)
        {
            case SoundCssStyle.Sing:
                acc.AddVibrato(5.5, 0.3);
                acc.GainDb += 1.0;
                acc.SpeedFactor *= 0.97;
                acc.Eq.Add(new EqBand(3000, +1.0, EqShelf.HighShelf));
                break;
            case SoundCssStyle.Whisper:
                acc.NoiseLayer += 0.5;
                acc.GainDb += -6.0;
                acc.Eq.Add(new EqBand(4000, +3.0, EqShelf.HighShelf));
                acc.Eq.Add(new EqBand(250, -4.0, EqShelf.LowShelf));
                break;
            case SoundCssStyle.Shout:
                acc.GainDb += 5.0;
                acc.Eq.Add(new EqBand(2000, +3.0, EqShelf.Peak));
                acc.Eq.Add(new EqBand(3500, +2.0, EqShelf.HighShelf));
                break;
            case SoundCssStyle.Normal:
            default:
                break;
        }
    }

    private static void ApplyAccent(Accumulator acc, SoundCssAccent accent)
    {
        switch (accent)
        {
            case SoundCssAccent.Uk:
                acc.FormantShift *= 1.02;
                acc.Eq.Add(new EqBand(2500, +1.0, EqShelf.Peak));
                break;
            case SoundCssAccent.India:
                acc.FormantShift *= 1.04;
                acc.Eq.Add(new EqBand(3000, +1.5, EqShelf.Peak));
                acc.SpeedFactor *= 1.03;
                break;
            case SoundCssAccent.Usa:
            default:
                break;
        }
    }

    private static void ApplyTimbre(Accumulator acc, SoundCssTimbre timbre)
    {
        switch (timbre)
        {
            case SoundCssTimbre.Bright:
                acc.Eq.Add(new EqBand(3500, +4.0, EqShelf.HighShelf));
                break;
            case SoundCssTimbre.Dark:
                acc.Eq.Add(new EqBand(250, +3.0, EqShelf.LowShelf));
                acc.Eq.Add(new EqBand(3500, -3.0, EqShelf.HighShelf));
                break;
            case SoundCssTimbre.Flat:
            default:
                break;
        }
    }

    private static void ApplyGender(Accumulator acc, SoundCssGender gender)
    {
        switch (gender)
        {
            case SoundCssGender.Male:
                acc.PitchSemitones += -4.0;
                acc.FormantShift *= 0.92;
                break;
            case SoundCssGender.Female:
                acc.PitchSemitones += 4.0;
                acc.FormantShift *= 1.08;
                break;
            case SoundCssGender.Neutral:
            default:
                break;
        }
    }

    private static void ApplyAge(Accumulator acc, SoundCssAge age)
    {
        switch (age)
        {
            case SoundCssAge.Child:
                acc.PitchSemitones += 5.0;
                acc.SpeedFactor *= 1.15;
                acc.FormantShift *= 1.15;
                break;
            case SoundCssAge.Teen:
                acc.PitchSemitones += 2.0;
                acc.FormantShift *= 1.05;
                break;
            case SoundCssAge.Senior:
                acc.PitchSemitones += -1.0;
                acc.GainDb += -1.0;
                acc.AddVibrato(4.0, 0.2);
                break;
            case SoundCssAge.Adult:
            default:
                break;
        }
    }

    private static void ApplyEmotion(Accumulator acc, SoundCssEmotion emotion)
    {
        switch (emotion)
        {
            case SoundCssEmotion.Happy:
                acc.PitchSemitones += 1.0;
                acc.SpeedFactor *= 1.05;
                acc.GainDb += 1.0;
                acc.AddVibrato(5.5, 0.15);
                break;
            case SoundCssEmotion.Sad:
                acc.PitchSemitones += -1.0;
                acc.SpeedFactor *= 0.92;
                acc.GainDb += -2.0;
                acc.Eq.Add(new EqBand(250, +2.0, EqShelf.LowShelf));
                acc.AddVibrato(4.0, 0.1);
                break;
            case SoundCssEmotion.Angry:
                acc.GainDb += 4.0;
                acc.SpeedFactor *= 1.05;
                acc.Eq.Add(new EqBand(2000, +3.0, EqShelf.Peak));
                break;
            case SoundCssEmotion.Calm:
                acc.GainDb += -1.0;
                acc.SpeedFactor *= 0.95;
                acc.Eq.Add(new EqBand(4000, -1.0, EqShelf.HighShelf));
                break;
            case SoundCssEmotion.Excited:
                acc.PitchSemitones += 2.0;
                acc.SpeedFactor *= 1.1;
                acc.GainDb += 3.0;
                acc.AddVibrato(6.0, 0.2);
                break;
        }
    }

    private static void ApplyBreath(Accumulator acc, SoundCssBreath breath)
    {
        switch (breath)
        {
            case SoundCssBreath.Low:
                acc.NoiseLayer += 0.1;
                break;
            case SoundCssBreath.Medium:
                acc.NoiseLayer += 0.25;
                break;
            case SoundCssBreath.High:
                acc.NoiseLayer += 0.45;
                acc.Eq.Add(new EqBand(4000, +2.0, EqShelf.HighShelf));
                break;
            case SoundCssBreath.None:
            default:
                break;
        }
    }

    private static void ApplyVibrato(Accumulator acc, SoundCssVibrato vibrato)
    {
        switch (vibrato)
        {
            case SoundCssVibrato.Light:
                acc.AddVibrato(5.5, 0.2);
                break;
            case SoundCssVibrato.Medium:
                acc.AddVibrato(5.5, 0.4);
                break;
            case SoundCssVibrato.Strong:
                acc.AddVibrato(6.0, 0.7);
                break;
            case SoundCssVibrato.None:
            default:
                break;
        }
    }

    private static double SpeedFactor(SoundCssSpeed speed) => speed.Mode switch
    {
        SoundCssSpeedMode.Fast => 1.15,
        SoundCssSpeedMode.Slow => 0.85,
        _ => speed.Multiplier ?? 1.0,
    };

    private static DspTransformPlan Finalize(Accumulator acc, CanonicalVoiceMetadata metadata)
    {
        var basePitch = metadata.BasePitchHz > 0 ? metadata.BasePitchHz : CanonicalVoiceMetadata.Default.BasePitchHz;

        // Pitch is computed relative to the canonical base and bounded to a human band.
        var targetHz = Math.Clamp(basePitch * Math.Pow(2.0, acc.PitchSemitones / 12.0), MinPitchHz, MaxPitchHz);
        var effectiveSemitones = 12.0 * Math.Log2(targetHz / basePitch);

        // Formant shift is bounded so the first formant stays in a plausible band.
        var formantShift = acc.FormantShift;
        var f1 = metadata.Formant1Hz > 0 ? metadata.Formant1Hz : CanonicalVoiceMetadata.Default.Formant1Hz;
        var shiftedF1 = f1 * formantShift;
        if (shiftedF1 > MaxFormantHz)
            formantShift = MaxFormantHz / f1;
        else if (shiftedF1 < MinFormantHz)
            formantShift = MinFormantHz / f1;
        formantShift = Math.Clamp(formantShift, 0.5, 2.0);

        var speedFactor = Math.Clamp(acc.SpeedFactor, 0.25, 4.0);
        var depth = Math.Clamp(acc.VibratoDepth, 0.0, 2.0);
        var rate = Math.Clamp(acc.VibratoRateHz, 0.0, 12.0);
        var vibrato = depth > 0 && rate > 0 ? new VibratoParams(rate, depth) : VibratoParams.None;

        return new DspTransformPlan
        {
            PitchSemitones = effectiveSemitones,
            TargetPitchHz = targetHz,
            TimeStretch = 1.0 / speedFactor,
            GainDb = Math.Clamp(acc.GainDb, -24.0, 24.0),
            EqBands = acc.Eq,
            FormantShift = formantShift,
            Vibrato = vibrato,
            NoiseLayer = Math.Clamp(acc.NoiseLayer, 0.0, 1.0),
        };
    }

    private sealed class Accumulator
    {
        public double PitchSemitones;
        public double SpeedFactor = 1.0;
        public double GainDb;
        public double FormantShift = 1.0;
        public double NoiseLayer;
        public double VibratoRateHz;
        public double VibratoDepth;
        public List<EqBand> Eq { get; } = [];

        public void AddVibrato(double rateHz, double depthSemitones)
        {
            if (depthSemitones <= 0)
                return;

            VibratoDepth += depthSemitones;
            VibratoRateHz = rateHz; // last active contribution (fixed order) sets the rate
        }
    }
}
