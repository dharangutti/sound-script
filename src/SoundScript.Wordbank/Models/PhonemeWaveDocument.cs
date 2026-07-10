namespace SoundScript.Wordbank.Models;

public sealed class PhonemeWaveDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public PhonemeFrequency Default { get; init; } = new();
    public PhonemeFrequency[] Phonemes { get; init; } = [];
}

public sealed class PhonemeFrequency
{
    public string? Phoneme { get; init; }
    public double MinHz { get; init; }
    public double MaxHz { get; init; }
    public double DurationBeats { get; init; }
    public string Class { get; init; } = "";
}
