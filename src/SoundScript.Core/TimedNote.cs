namespace SoundScript.Core;

public readonly record struct TimedNote(
    int MidiNumber,
    double StartBeat,
    double DurationBeats,
    double DurationMs,
    int Velocity = 64,
    byte Channel = 0);
