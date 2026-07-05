// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Wave.Mixing;

/// <summary>
/// Sums rendered notes into a per-track buffer, then sums tracks into a
/// final buffer with a simple peak-normalize pass to prevent clipping — no
/// dynamic range compression, no effects. Summation always iterates in the
/// caller-supplied list order (never re-sorted), so the result is
/// deterministic: identical input always produces byte-identical output.
/// </summary>
public static class Mixer
{
    public static float[] RenderTrack(IReadOnlyList<NoteEvent> notes, int sampleRate)
    {
        if (notes.Count == 0)
            return [];

        var rendered = new (int StartSample, float[] Samples)[notes.Count];
        var totalLength = 0;

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var samples = NoteRenderer.Render(note, sampleRate);
            var startSample = Math.Max(0, (int)Math.Round(note.StartTimeSeconds * sampleRate));
            rendered[i] = (startSample, samples);
            totalLength = Math.Max(totalLength, startSample + samples.Length);
        }

        var buffer = new double[totalLength];
        foreach (var (startSample, samples) in rendered)
        {
            for (var i = 0; i < samples.Length; i++)
                buffer[startSample + i] += samples[i];
        }

        return ToFloatArray(buffer);
    }

    public static float[] MixTracks(IReadOnlyList<float[]> trackBuffers)
    {
        var totalLength = 0;
        foreach (var track in trackBuffers)
            totalLength = Math.Max(totalLength, track.Length);

        var mixed = new double[totalLength];
        foreach (var track in trackBuffers)
        {
            for (var i = 0; i < track.Length; i++)
                mixed[i] += track[i];
        }

        return NormalizePeak(mixed);
    }

    private static float[] NormalizePeak(double[] buffer)
    {
        if (buffer.Length == 0)
            return [];

        var peak = 0.0;
        foreach (var sample in buffer)
            peak = Math.Max(peak, Math.Abs(sample));

        // Only scale down when the mix would otherwise clip — quiet passages
        // are left alone rather than maximized (that's dynamic range
        // compression, an explicit v1 non-goal).
        var scale = peak > 1.0 ? 1.0 / peak : 1.0;

        var result = new float[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = (float)(buffer[i] * scale);

        return result;
    }

    private static float[] ToFloatArray(double[] buffer)
    {
        var result = new float[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
            result[i] = (float)buffer[i];

        return result;
    }
}
