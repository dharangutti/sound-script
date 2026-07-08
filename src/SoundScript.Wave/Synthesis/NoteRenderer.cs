// UNDER DEVELOPMENT — v2
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Renders a single <see cref="NoteEvent"/> into its own sample buffer
/// (oscillator × envelope × velocity), sized to cover the note's sustain
/// plus its release tail. Purely sequential phase accumulation — no
/// randomness, no parallelism, so identical input always yields identical
/// output.
///
/// v2 default path: band-limited wavetable lookup driven by a 32-bit
/// fixed-point phase accumulator (see <see cref="Wavetable"/>), replacing
/// v1's per-sample Math.Sin and float phase. The v1 path survives behind
/// <see cref="WaveEngineOptions.UseLegacyTrigOscillator"/> for transition-
/// period A/B comparison only.
/// </summary>
public static class NoteRenderer
{
    public static float[] Render(NoteEvent note, int sampleRate) =>
        Render(note, sampleRate, WaveEngineOptions.UseLegacyTrigOscillator);

    /// <summary>
    /// Internal overload taking the engine choice explicitly so tests can
    /// A/B the two paths without mutating shared static state (xUnit runs
    /// test classes in parallel — flipping the global flag mid-suite could
    /// race against unrelated rendering tests).
    /// </summary>
    internal static float[] Render(NoteEvent note, int sampleRate, bool useLegacyTrigOscillator)
    {
        var release = Math.Max(0.0, note.Timbre.Envelope.Release);
        var totalSeconds = Math.Max(0.0, note.DurationSeconds) + release;
        var sampleCount = Math.Max(0, (int)Math.Ceiling(totalSeconds * sampleRate));
        var buffer = new float[sampleCount];

        if (sampleCount == 0)
            return buffer;

        var detuneRatio = Math.Pow(2.0, note.Timbre.DetuneCents / 1200.0);
        var frequency = note.FrequencyHz * detuneRatio;
        var velocity = Math.Clamp(note.Velocity, 0.0, 1.0);

        if (useLegacyTrigOscillator)
            RenderLegacyTrig(note, sampleRate, frequency, velocity, buffer);
        else
            RenderWavetable(note, sampleRate, frequency, velocity, buffer);

        return buffer;
    }

    private static void RenderWavetable(NoteEvent note, int sampleRate, double frequency, double velocity, float[] buffer)
    {
        if (note.Timbre.Oscillator == OscillatorType.Noise)
        {
            RenderNoise(note, sampleRate, frequency, velocity, buffer);
            return;
        }

        var table = Wavetable.GetTable(note.Timbre.Oscillator, frequency);
        var phaseIncrement = Wavetable.PhaseIncrement(frequency, sampleRate);

        // uint addition wraps modulo 2^32 by definition (C# default unchecked
        // context) — deterministic cycle wrapping with zero float drift,
        // replacing v1's `phase -= Math.Floor(phase)` trick.
        var phase = 0u;
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)sampleRate;
            var amplitude = Envelope.Amplitude(note.Timbre.Envelope, t, note.DurationSeconds);
            buffer[i] = (float)(Wavetable.Sample(table, phase) * amplitude * velocity);

            phase += phaseIncrement;
        }
    }

    // Distinct seed derivation per note (start time + frequency) rather than a
    // shared salt on a passed-in seed — Noise has no NoteEvent-level seed
    // field, so this keeps two notes at different times/pitches decorrelated
    // without any schema change.
    private const int NoiseSalt = 0;

    /// <summary>
    /// Deterministic filtered white noise for <see cref="OscillatorType.Noise"/>
    /// (prosody plosives/fricatives — see PhonemeFrequencyTable): draws a
    /// fresh uniform sample per output sample from
    /// <see cref="DeterministicRandom"/>, then shapes it with the same
    /// one-pole low-pass recurrence as <c>Effects.OnePoleFilter</c>
    /// (alpha = w/(1+w), cutoff = <paramref name="frequency"/>) applied
    /// inline so noise-class prosody tones need no separate post-processing
    /// pass through the mixer.
    /// </summary>
    private static void RenderNoise(NoteEvent note, int sampleRate, double frequency, double velocity, float[] buffer)
    {
        var seed = DeterministicRandom.DeriveSeed(
            note.StartTimeSeconds.ToString("R") + "|" + frequency.ToString("R"));
        var w = 2.0 * Math.PI * frequency / sampleRate;
        var alpha = w / (1.0 + w);
        var y = 0.0;

        for (var i = 0; i < buffer.Length; i++)
        {
            var raw = DeterministicRandom.Unit(seed, i, NoiseSalt);
            y += alpha * (raw - y);

            var t = i / (double)sampleRate;
            var amplitude = Envelope.Amplitude(note.Timbre.Envelope, t, note.DurationSeconds);
            buffer[i] = (float)(y * amplitude * velocity);
        }
    }

    // The v1 loop, byte-for-byte behavior — kept only for A/B comparison
    // against the wavetable path (see WaveEngineOptions), not as a permanent
    // dual implementation.
    private static void RenderLegacyTrig(NoteEvent note, int sampleRate, double frequency, double velocity, float[] buffer)
    {
        var phaseIncrement = frequency / sampleRate;

        var phase = 0.0;
        for (var i = 0; i < buffer.Length; i++)
        {
            var t = i / (double)sampleRate;
            var amplitude = Envelope.Amplitude(note.Timbre.Envelope, t, note.DurationSeconds);
            buffer[i] = (float)(Oscillator.Sample(note.Timbre.Oscillator, phase) * amplitude * velocity);

            phase += phaseIncrement;
            phase -= Math.Floor(phase);
        }
    }
}
