using SoundScript.Core.Notation;
using SoundScript.Wordbank;
using SoundScript.Wordbank.Models;

namespace SoundScript.Timbre;

/// <summary>
/// Builds a <see cref="TimbreTimeline"/> from grapheme-driven phonemes for offline
/// wordbank G2P vocal synthesis (no MIDI step).
/// </summary>
public static class PhonemeVocalTimeline
{
    public const int DefaultSampleRate = 44100;
    public const double DefaultFrameMs = 8.0;
    public const double DefaultTempoBpm = 120;

    /// <summary>Builds a deterministic timbre timeline for a phoneme sequence.</summary>
    public static TimbreTimeline Build(
        IReadOnlyList<string> phonemes,
        int sampleRate = DefaultSampleRate,
        double frameMs = DefaultFrameMs,
        double tempoBpm = DefaultTempoBpm,
        double durationScale = 1.0)
    {
        var locale = WordbankCatalog.Active;
        var segments = new List<TimbreSegment>(phonemes.Count);
        var startMs = 0.0;

        foreach (var phoneme in phonemes)
        {
            var wave = locale.WaveFrequencyMap.TryGetValue(phoneme, out var frequency)
                ? frequency
                : locale.PhonemeWave.Default;

            var classScale = wave.Class switch
            {
                "vowel" => 2.0,
                "nasal" or "liquid" => 1.6,
                "fricative" => 1.35,
                _ => 1.5,
            };

            var durationMs = BeatsToMilliseconds(wave.DurationBeats * classScale * durationScale, tempoBpm);
            var pitchHz = (wave.MinHz + wave.MaxHz) * 0.5;
            var velocity = VelocityForClass(wave.Class);

            segments.Add(new TimbreSegment(
                startMs,
                durationMs,
                pitchHz,
                velocity,
                phoneme,
                PhonemeTimbreMapper.Map(phoneme)));

            startMs += durationMs;
        }

        var totalDurationMs = segments.Count == 0
            ? 0
            : segments.Max(segment => segment.StartMs + segment.DurationMs) + frameMs;

        var frames = MidiToTimbreTimeline.BuildFramePlans(segments, frameMs, totalDurationMs);

        return new TimbreTimeline
        {
            SampleRate = sampleRate,
            FrameMs = frameMs,
            TotalDurationMs = totalDurationMs,
            Segments = segments,
            Frames = frames,
        };
    }

    private static double BeatsToMilliseconds(double beats, double tempoBpm) =>
        beats * 60_000.0 / tempoBpm;

    private static int VelocityForClass(string phonemeClass) => phonemeClass switch
    {
        "vowel" => 88,
        "fricative" => 72,
        "plosive" => 68,
        _ => 76,
    };
}
