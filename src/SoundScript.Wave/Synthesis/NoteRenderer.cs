// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Wave.Model;

namespace SoundScript.Wave.Synthesis;

/// <summary>
/// Renders a single <see cref="NoteEvent"/> into its own sample buffer
/// (oscillator × envelope × velocity), sized to cover the note's sustain
/// plus its release tail. Purely sequential phase accumulation — no
/// randomness, no parallelism, so identical input always yields identical
/// output.
/// </summary>
public static class NoteRenderer
{
    public static float[] Render(NoteEvent note, int sampleRate)
    {
        var release = Math.Max(0.0, note.Timbre.Envelope.Release);
        var totalSeconds = Math.Max(0.0, note.DurationSeconds) + release;
        var sampleCount = Math.Max(0, (int)Math.Ceiling(totalSeconds * sampleRate));
        var buffer = new float[sampleCount];

        if (sampleCount == 0)
            return buffer;

        var detuneRatio = Math.Pow(2.0, note.Timbre.DetuneCents / 1200.0);
        var frequency = note.FrequencyHz * detuneRatio;
        var phaseIncrement = frequency / sampleRate;
        var velocity = Math.Clamp(note.Velocity, 0.0, 1.0);

        var phase = 0.0;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var amplitude = Envelope.Amplitude(note.Timbre.Envelope, t, note.DurationSeconds);
            buffer[i] = (float)(Oscillator.Sample(note.Timbre.Oscillator, phase) * amplitude * velocity);

            phase += phaseIncrement;
            phase -= Math.Floor(phase);
        }

        return buffer;
    }
}
