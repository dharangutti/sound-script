namespace SoundScript.Prosody;

/// <summary>One word and its syllable breakdown, preserving original casing.</summary>
public readonly record struct WordUnit(string Word, IReadOnlyList<string> Syllables);
