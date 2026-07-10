namespace SoundScript.Wordbank.Models;

public sealed class PhonemeComposeDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public PhonemeGesture DefaultGesture { get; init; } = new();
    public PhonemeGesture[] Phonemes { get; init; } = [];
}

public sealed class PhonemeGesture
{
    public string? Phoneme { get; init; }
    public string Kind { get; init; } = "";
    public string Pitch { get; init; } = "";
    public int Octave { get; init; }
    public string Duration { get; init; } = "";
}
