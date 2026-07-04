namespace SoundScript.Timbre;

/// <summary>
/// Cycle-accurate synthesis plan for one 8 ms frame. Pitch determines how many
/// waveform cycles fit inside the frame (clamped to 3–10).
/// </summary>
public sealed record TimbreFramePlan(
    int FrameIndex,
    double StartMs,
    double NoteStartMs,
    double NoteDurationMs,
    double PitchHz,
    int Velocity,
    string Phoneme,
    TimbreProfile Profile,
    int CycleCount,
    double CycleLengthMs);

/// <summary>
/// Computes cycle counts and frame plans from pitch and frame duration.
/// </summary>
public static class CyclePlanner
{
    public const int MinCyclesPerFrame = 3;
    public const int MaxCyclesPerFrame = 10;

    /// <summary>
    /// Number of pitch cycles that fit in one frame, clamped to [3, 10].
    /// </summary>
    public static int CycleCountForFrame(double pitchHz, double frameMs)
    {
        if (pitchHz <= 0)
            return MinCyclesPerFrame;

        var cycleLengthMs = CycleGenerator.CycleLengthMs(pitchHz);
        var raw = (int)Math.Round(frameMs / cycleLengthMs);
        return Math.Clamp(raw, MinCyclesPerFrame, MaxCyclesPerFrame);
    }
}
