// UNDER DEVELOPMENT — v3
using System.Globalization;
using SoundScript.Core.Ast;
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Adapter;

/// <summary>
/// Converts the program's top-level <see cref="EffectNode"/>s (raw string
/// parameters, in file order) into typed <see cref="EffectSettings"/> for
/// <c>SoundScript.Wave.Effects.MasterEffectChain</c>. Lives in the Adapter
/// namespace because it reads AST types — the Effects DSP itself never does
/// (same layering rule as AstToNoteEventAdapter/NoteEvent).
///
/// The parser has already validated kinds, keys, and ranges, so failures here
/// only occur for hand-built ASTs; they still throw descriptive errors rather
/// than producing silent defaults.
/// </summary>
public static class EffectSettingsFactory
{
    private const double DefaultFeedback = 0.0;
    private const double DefaultMix = 0.5;

    public static List<EffectSettings> FromProgram(ProgramNode program)
    {
        var effects = new List<EffectSettings>();
        foreach (var statement in program.Statements)
        {
            if (statement is EffectNode effect)
                effects.Add(Convert(effect));
        }

        return effects;
    }

    private static EffectSettings Convert(EffectNode effect) => effect.Kind.ToLowerInvariant() switch
    {
        EffectKinds.Delay => new DelaySettings(
            TimeSeconds: GetRequiredDouble(effect, "time"),
            Feedback: GetOptionalDouble(effect, "feedback", DefaultFeedback),
            Mix: GetOptionalDouble(effect, "mix", DefaultMix)),

        EffectKinds.Filter => new FilterSettings(
            Kind: GetFilterKind(effect),
            CutoffHz: GetRequiredDouble(effect, "cutoff")),

        _ => throw new NotSupportedException(
            $"Unknown effect kind '{effect.Kind}' — supported in v3: {EffectKinds.SupportedListText}.")
    };

    private static FilterKind GetFilterKind(EffectNode effect)
    {
        var type = GetRequiredText(effect, "type").ToLowerInvariant();
        return type switch
        {
            "lowpass" => FilterKind.LowPass,
            "highpass" => FilterKind.HighPass,
            _ => throw new InvalidOperationException(
                $"Unknown filter type '{type}' — supported: lowpass, highpass.")
        };
    }

    private static string GetRequiredText(EffectNode effect, string key)
    {
        if (!effect.Parameters.TryGetValue(key, out var text))
            throw new InvalidOperationException($"effect {effect.Kind} is missing required parameter '{key}='.");

        return text;
    }

    private static double GetRequiredDouble(EffectNode effect, string key) =>
        ParseDouble(effect, key, GetRequiredText(effect, key));

    private static double GetOptionalDouble(EffectNode effect, string key, double fallback) =>
        effect.Parameters.TryGetValue(key, out var text) ? ParseDouble(effect, key, text) : fallback;

    // InvariantCulture on purpose — parameter values must mean the same thing
    // on every machine/locale (determinism safeguard).
    private static double ParseDouble(EffectNode effect, string key, string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"effect {effect.Kind} parameter '{key}={text}' is not a number.");

        return value;
    }
}
