// UNDER DEVELOPMENT — v1 prototype
namespace SoundScript.Wave.Model;

/// <summary>
/// A single sounding event in physical units — no MIDI numbers, no ticks.
/// This is the sole hand-off point between SoundScript.Wave.Adapter (which
/// depends on the AST) and everything downstream (which does not).
/// </summary>
public record NoteEvent(
    double FrequencyHz,
    double StartTimeSeconds,
    double DurationSeconds,
    double Velocity,     // 0.0-1.0
    TimbreParams Timbre
);
