namespace SoundScript.Core;

public readonly record struct TimedNote(int MidiNumber, double StartBeat, double DurationBeats);
