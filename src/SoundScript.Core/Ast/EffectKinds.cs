// UNDER DEVELOPMENT — v3
namespace SoundScript.Core.Ast;

/// <summary>
/// The effect kinds the grammar recognizes as of v3. Shared by
/// SoundScript.Parser (validates the kind/keys/ranges at parse time) and
/// SoundScript.Wave (converts <see cref="EffectNode"/> to typed DSP
/// settings), so the two hardcoded lists can't silently drift out of sync —
/// see EffectNode's summary for the parser/wave split.
/// </summary>
public static class EffectKinds
{
    public const string Delay = "delay";
    public const string Filter = "filter";

    /// <summary>All kinds currently accepted, for error messages and test coverage.</summary>
    public static readonly IReadOnlyList<string> All = [Delay, Filter];

    public static readonly string SupportedListText = string.Join(", ", All);
}
