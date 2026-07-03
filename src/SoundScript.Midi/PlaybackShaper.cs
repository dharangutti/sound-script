using SoundScript.Core;
using SoundScript.Core.Notation;

namespace SoundScript.Midi;

internal readonly record struct PlaybackShapeResult(
    int Velocity,
    double DurationBeats,
    bool DynamicShaped,
    bool ArticulationShaped,
    bool GainRefined,
    bool DurationNormalized,
    bool ExpressiveApplied);

/// <summary>Final playback shaping pipeline before MIDI emission.</summary>
internal static class PlaybackShaper
{
    internal static PlaybackShapeResult ShapeNote(
        int? noteVelocity,
        int? rampVelocity,
        DynamicLevel? noteDynamic,
        DynamicLevel? trackDynamic,
        int trackVelocity,
        ArticulationType? articulation,
        string? instrumentName,
        double writtenDurationBeats)
    {
        var effectiveDynamic = noteDynamic ?? trackDynamic;
        var baseVelocity = noteVelocity
            ?? rampVelocity
            ?? effectiveDynamic?.ToVelocity()
            ?? trackVelocity;

        var (dynamicVelocity, dynamicShaped) = DynamicShaper.Apply(effectiveDynamic, baseVelocity);

        var articulationShape = ArticulationShaper.Apply(articulation, dynamicVelocity, writtenDurationBeats);
        var velocity = articulationShape.Velocity;
        var duration = articulationShape.DurationBeats;

        velocity = Math.Clamp((int)Math.Round(velocity * InstrumentGainMap.GetGain(instrumentName)), 1, 127);

        var (refinedVelocity, gainRefined) = InstrumentGainRefiner.Apply(instrumentName, velocity);
        velocity = refinedVelocity;

        var (expressiveVelocity, expressiveApplied) = ExpressiveCurve.Apply(velocity, articulation);
        velocity = expressiveVelocity;

        var (normalizedDuration, durationNormalized) = DurationNormalizer.Apply(duration);

        return new PlaybackShapeResult(
            velocity,
            normalizedDuration,
            dynamicShaped,
            articulationShape.Shaped,
            gainRefined,
            durationNormalized,
            expressiveApplied);
    }

    internal static PlaybackShapeResult ShapeChordVelocity(
        int? chordVelocity,
        DynamicLevel? trackDynamic,
        int trackVelocity,
        string? instrumentName)
    {
        var effectiveDynamic = trackDynamic;
        var baseVelocity = chordVelocity
            ?? effectiveDynamic?.ToVelocity()
            ?? trackVelocity;

        var (dynamicVelocity, dynamicShaped) = DynamicShaper.Apply(effectiveDynamic, baseVelocity);
        var velocity = Math.Clamp((int)Math.Round(dynamicVelocity * InstrumentGainMap.GetGain(instrumentName)), 1, 127);
        var (refinedVelocity, gainRefined) = InstrumentGainRefiner.Apply(instrumentName, velocity);
        var (expressiveVelocity, expressiveApplied) = ExpressiveCurve.Apply(refinedVelocity, null);

        return new PlaybackShapeResult(
            expressiveVelocity,
            0,
            dynamicShaped,
            false,
            gainRefined,
            false,
            expressiveApplied);
    }
}
