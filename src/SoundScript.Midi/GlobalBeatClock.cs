namespace SoundScript.Midi;

/// <summary>Global beat clock shared across all tracks for perfect multi-track alignment.</summary>
internal sealed class GlobalBeatClock
{
    private bool _syncCorrectionApplied;

    internal double ToGlobalBeat(double trackBeat, double trackOffset) =>
        BeatMath.RoundBeat(trackBeat + trackOffset);

    internal double NormalizeTrackBeat(double globalBeat, double trackOffset) =>
        BeatMath.RoundBeat(globalBeat - trackOffset);

    internal bool MarkSyncCorrection()
    {
        if (_syncCorrectionApplied)
            return false;

        _syncCorrectionApplied = true;
        return true;
    }
}
