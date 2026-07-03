namespace SoundScript.Midi;

/// <summary>Deterministic beat math to prevent floating-point drift.</summary>
internal static class BeatMath
{
    internal const double Epsilon = 1e-9;

    internal static double RoundBeat(double beats) =>
        Math.Round(beats, 9, MidpointRounding.AwayFromZero);

    internal static double AddBeats(double left, double right) =>
        RoundBeat(left + right);
}
