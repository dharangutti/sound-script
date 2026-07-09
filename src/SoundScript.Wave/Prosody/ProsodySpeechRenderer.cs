// UNDER DEVELOPMENT — v8 offline vocal stems
using SoundScript.Core;
using SoundScript.Wave.Io;
using SoundScript.Wave.Mixing;
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Wave.Prosody;

/// <summary>
/// Renders a standalone <c>speak</c> phrase to mono samples for offline vocal
/// stems (<c>soundscript vocal generate|batch --engine prosody</c>). Uses longer
/// phonemes and tighter gaps than in-mix prosody so the result reads clearly as
/// synthetic speech-like audio (not human TTS — install espeak-ng for that).
/// </summary>
public static class ProsodySpeechRenderer
{
    private const double VowelFormantRatio = 2.5;
    private const double VowelFormantVelocityScale = 0.62;
    private const double VowelThirdHarmonicRatio = 4.0;
    private const double VowelThirdHarmonicVelocityScale = 0.28;

    private static readonly Adsr PlosiveEnvelope = new(Attack: 0.002, Decay: 0.015, Sustain: 0.0, Release: 0.01);
    private static readonly Adsr FricativeEnvelope = new(Attack: 0.008, Decay: 0.03, Sustain: 0.72, Release: 0.06);

    public static float[] RenderStem(string text, int seed, int tempoBpm = 120)
    {
        var tempoMap = new TempoAutomationMap();
        tempoMap.SetTempo(0, tempoBpm);

        var tones = ProsodyToneGenerator.GenerateForVocalStem(text, seed);
        var notes = new List<NoteEvent>();
        var beat = 0.0;

        foreach (var tone in tones)
        {
            if (!tone.IsRest)
            {
                var startSeconds = tempoMap.BeatsToMilliseconds(0, beat) / 1000.0;
                var durationSeconds = tempoMap.BeatsToMilliseconds(beat, tone.DurationBeats) / 1000.0;
                notes.AddRange(BuildStemNotes(tone, startSeconds, durationSeconds));
            }

            beat += tone.DurationBeats;
        }

        if (notes.Count == 0)
            return [];

        return Mixer.RenderTrack(notes, WavWriter.SampleRate);
    }

    private static IEnumerable<NoteEvent> BuildStemNotes(ProsodyTone tone, double startSeconds, double durationSeconds)
    {
        switch (tone.Class)
        {
            case PhonemeClass.Vowel:
                yield return StemNote(tone.FrequencyHz, startSeconds, durationSeconds, tone.Velocity,
                    TimbreParams.Default with { Oscillator = OscillatorType.Triangle });
                yield return StemNote(tone.FrequencyHz * VowelFormantRatio, startSeconds, durationSeconds,
                    tone.Velocity * VowelFormantVelocityScale, TimbreParams.Default);
                yield return StemNote(tone.FrequencyHz * VowelThirdHarmonicRatio, startSeconds, durationSeconds,
                    tone.Velocity * VowelThirdHarmonicVelocityScale, TimbreParams.Default);
                break;

            case PhonemeClass.Plosive:
                yield return StemNote(tone.FrequencyHz, startSeconds, durationSeconds, tone.Velocity,
                    TimbreParams.Default with { Oscillator = OscillatorType.Noise, Envelope = PlosiveEnvelope });
                break;

            case PhonemeClass.Fricative:
                yield return StemNote(tone.FrequencyHz, startSeconds, durationSeconds, tone.Velocity * 1.15,
                    TimbreParams.Default with { Oscillator = OscillatorType.Noise, Envelope = FricativeEnvelope });
                break;

            case PhonemeClass.Nasal:
            case PhonemeClass.Liquid:
            default:
                yield return StemNote(tone.FrequencyHz, startSeconds, durationSeconds, tone.Velocity,
                    TimbreParams.Default with { Oscillator = OscillatorType.Triangle });
                break;
        }
    }

    private static NoteEvent StemNote(
        double frequencyHz,
        double startSeconds,
        double durationSeconds,
        double velocity,
        TimbreParams timbre) =>
        new(
            FrequencyHz: frequencyHz,
            StartTimeSeconds: startSeconds,
            DurationSeconds: durationSeconds,
            Velocity: Math.Clamp(velocity, 0.0, 1.0),
            Timbre: timbre);
}
