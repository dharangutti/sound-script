namespace SoundScript.Core;

/// <summary>
/// Result of interpreting one voice block: lyric syllables bound to timed pitches.
/// Lives beside <see cref="InterpretedTrack"/> without touching it — the vocal
/// subsystem is a parallel pipeline branch.
/// </summary>
public sealed class InterpretedVocalTrack
{
    public string Name { get; init; } = "voice";
    public int ProgramNumber { get; set; } = VocalTimbreMap.DefaultProgram;
    public List<TimedSyllable> Syllables { get; } = [];
}

/// <summary>
/// One sung event. <see cref="Text"/> is empty for melisma continuations
/// (a vowel held across additional notes). <see cref="IsWordEnd"/> follows the
/// standard karaoke convention: word-final syllables get a trailing space in
/// the exported MIDI lyric event.
/// </summary>
public readonly record struct TimedSyllable(
    string Text,
    bool IsWordEnd,
    int MidiNumber,
    double StartBeat,
    double DurationBeats,
    double DurationMs,
    int Velocity,
    bool IsMelisma);
