namespace SoundScript.Wordbank.Models;

public sealed class WordProsodyDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public int CenterMidi { get; init; }
    public Dictionary<string, PositionOffsets> WordPitchOffsets { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, RampContour> PhraseContours { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> SyllableStressOffsets { get; init; } = new(StringComparer.Ordinal);
    public ClampSettings Clamp { get; init; } = new();
}

public sealed class PositionOffsets
{
    public int Start { get; init; }
    public int Middle { get; init; }
    public int End { get; init; }
}

public sealed class RampContour
{
    public int From { get; init; }
    public int To { get; init; }
    public int? Step { get; init; }
    public int? Max { get; init; }
}

public sealed class ClampSettings
{
    public int MaxAdjacentJump { get; init; }
    public int MaxPhraseRange { get; init; }
}
