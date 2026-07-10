using System.Globalization;

namespace SoundScript.Timbre;

/// <summary>Vocal delivery style at word scope.</summary>
public enum SoundCssStyle { Normal, Sing, Whisper, Shout }

/// <summary>Regional accent target.</summary>
public enum SoundCssAccent { Usa, Uk, India }

/// <summary>How a <see cref="SoundCssSpeed"/> value was expressed.</summary>
public enum SoundCssSpeedMode { Fast, Slow, Explicit }

/// <summary>Loudness/effort level.</summary>
public enum SoundCssEnergy { High, Medium, Low }

/// <summary>Spectral colour hint.</summary>
public enum SoundCssTimbre { Bright, Dark, Flat }

/// <summary>Voice gender target.</summary>
public enum SoundCssGender { Male, Female, Neutral }

/// <summary>Voice age target.</summary>
public enum SoundCssAge { Child, Teen, Adult, Senior }

/// <summary>Named persona preset (bundled transform intent; concrete DSP mapped in Prompt 4).</summary>
public enum SoundCssPersona { Narrator, Robot, Soft, Bright }

/// <summary>Emotional colouring.</summary>
public enum SoundCssEmotion { Happy, Sad, Angry, Calm, Excited }

/// <summary>Breathiness amount.</summary>
public enum SoundCssBreath { None, Low, Medium, High }

/// <summary>Vibrato depth.</summary>
public enum SoundCssVibrato { None, Light, Medium, Strong }

/// <summary>
/// Word-level speed control. <see cref="SoundCssSpeedMode.Fast"/>/<see cref="SoundCssSpeedMode.Slow"/>
/// are keyword presets (concrete factor decided by the DSP mapping layer); <see cref="SoundCssSpeedMode.Explicit"/>
/// carries a caller-supplied multiplier (e.g. <c>x1.2</c>).
/// </summary>
public sealed record SoundCssSpeed(SoundCssSpeedMode Mode, double? Multiplier)
{
    /// <summary>Canonical token, e.g. <c>fast</c>, <c>slow</c>, or <c>x1.2</c>.</summary>
    public string Token => Mode switch
    {
        SoundCssSpeedMode.Fast => "fast",
        SoundCssSpeedMode.Slow => "slow",
        _ => "x" + (Multiplier ?? 1.0).ToString(CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// Validated word-level pronunciation controls parsed from a SoundCSS word rule
/// (e.g. <c>"hello" { style: sing; pitch: +2; }</c>). Unset attributes are null.
/// Pure data — deterministic, no randomness.
/// </summary>
public sealed record SoundCssPronunciation
{
    /// <summary>The word this rule applies to (selector text without quotes).</summary>
    public string Word { get; init; } = "";

    public SoundCssStyle? Style { get; init; }
    public SoundCssAccent? Accent { get; init; }
    public SoundCssSpeed? Speed { get; init; }

    /// <summary>Pitch offset in semitones (range −24..24).</summary>
    public double? PitchSemitones { get; init; }

    public SoundCssEnergy? Energy { get; init; }
    public SoundCssTimbre? Timbre { get; init; }
    public SoundCssGender? Gender { get; init; }
    public SoundCssAge? Age { get; init; }
    public SoundCssPersona? Persona { get; init; }
    public SoundCssEmotion? Emotion { get; init; }
    public SoundCssBreath? Breath { get; init; }
    public SoundCssVibrato? Vibrato { get; init; }

    /// <summary>
    /// Projects this pronunciation into a deterministic <see cref="TransformPlan"/>
    /// for the rendering pipeline. Directives are emitted in a fixed
    /// <see cref="TransformKind"/> order, so identical input yields an identical plan.
    /// </summary>
    public TransformPlan ToTransformPlan()
    {
        var directives = new List<TransformDirective>();

        if (Style is { } style)
            directives.Add(new TransformDirective(TransformKind.Style, Token(style)));
        if (Accent is { } accent)
            directives.Add(new TransformDirective(TransformKind.Accent, Token(accent)));
        if (Speed is { } speed)
            directives.Add(new TransformDirective(TransformKind.Speed, speed.Token, speed.Multiplier));
        if (PitchSemitones is { } pitch)
            directives.Add(new TransformDirective(TransformKind.Pitch, FormatPitch(pitch), pitch));
        if (Energy is { } energy)
            directives.Add(new TransformDirective(TransformKind.Energy, Token(energy)));
        if (Timbre is { } timbre)
            directives.Add(new TransformDirective(TransformKind.Timbre, Token(timbre)));
        if (Gender is { } gender)
            directives.Add(new TransformDirective(TransformKind.Gender, Token(gender)));
        if (Age is { } age)
            directives.Add(new TransformDirective(TransformKind.Age, Token(age)));
        if (Persona is { } persona)
            directives.Add(new TransformDirective(TransformKind.Persona, Token(persona)));
        if (Emotion is { } emotion)
            directives.Add(new TransformDirective(TransformKind.Emotion, Token(emotion)));
        if (Breath is { } breath)
            directives.Add(new TransformDirective(TransformKind.Breath, Token(breath)));
        if (Vibrato is { } vibrato)
            directives.Add(new TransformDirective(TransformKind.Vibrato, Token(vibrato)));

        return new TransformPlan(Word, directives);
    }

    private static string Token<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static string FormatPitch(double semitones) =>
        (semitones >= 0 ? "+" : "") + semitones.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Categories of word-level transform, in canonical emission order.</summary>
public enum TransformKind
{
    Style,
    Accent,
    Speed,
    Pitch,
    Energy,
    Timbre,
    Gender,
    Age,
    Persona,
    Emotion,
    Breath,
    Vibrato,
}

/// <summary>
/// A single normalized transform directive. <see cref="Value"/> is the canonical
/// token (e.g. <c>sing</c>, <c>x1.2</c>, <c>+2</c>); <see cref="Numeric"/> carries
/// the parsed number for speed multipliers and pitch offsets, else null.
/// </summary>
public sealed record TransformDirective(TransformKind Kind, string Value, double? Numeric = null);

/// <summary>
/// Deterministic, DSP-agnostic plan for a single word. Prompt 4 maps these
/// directives to concrete DSP parameters and presets.
/// </summary>
public sealed record TransformPlan(string Word, IReadOnlyList<TransformDirective> Directives);
