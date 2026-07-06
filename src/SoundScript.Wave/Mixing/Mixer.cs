// UNDER DEVELOPMENT — v2
using SoundScript.Wave.Model;
using SoundScript.Wave.Synthesis;

namespace SoundScript.Wave.Mixing;

/// <summary>
/// Sums rendered notes into a per-track buffer, then sums tracks into a
/// final buffer with a simple peak-normalize pass to prevent clipping — no
/// dynamic range compression, no effects. Summation always iterates in the
/// caller-supplied list order (never re-sorted), so the result is
/// deterministic: identical input always produces byte-identical output.
///
/// The v1 mono methods (<see cref="RenderTrack"/>/<see cref="MixTracks"/>)
/// are unchanged; v2 adds parallel stereo methods that honor
/// <see cref="TimbreParams.Pan"/> via constant-power gains.
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

    /// <summary>
    /// Stereo counterpart of <see cref="RenderTrack"/>: each note is rendered
    /// mono, then placed in the stereo field by its
    /// <see cref="TimbreParams.Pan"/> via constant-power gains (see
    /// <see cref="PanGains"/>).
    ///
    /// Scope note (a deliberate decision, not an oversight): no .ss/.ssw
    /// grammar directive for pan exists, and adding one would require
    /// modifying SoundScript.Core (the AST node types) or SoundScript.Parser,
    /// which the safeguards forbid. AstToNoteEventAdapter therefore still
    /// emits <see cref="TimbreParams.Default"/> (Pan = 0.0) for every note,
    /// exactly as in v1, so parsed programs render dead-center. Panning is
    /// wired end-to-end below the adapter — mixer through stereo WAV writer —
    /// so any <see cref="NoteEvent"/> constructed with a non-zero Pan (direct
    /// API callers, tests) pans correctly today, and a future grammar
    /// addition needs no further plumbing changes here.
    /// </summary>
    public static (float[] Left, float[] Right) RenderTrackStereo(IReadOnlyList<NoteEvent> notes, int sampleRate)
    {
        if (notes.Count == 0)
            return ([], []);

        var rendered = new (int StartSample, float[] Samples, double LeftGain, double RightGain)[notes.Count];
        var totalLength = 0;

        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            var samples = NoteRenderer.Render(note, sampleRate);
            var startSample = Math.Max(0, (int)Math.Round(note.StartTimeSeconds * sampleRate));
            var (leftGain, rightGain) = PanGains(note.Timbre.Pan);
            rendered[i] = (startSample, samples, leftGain, rightGain);
            totalLength = Math.Max(totalLength, startSample + samples.Length);
        }

        var left = new double[totalLength];
        var right = new double[totalLength];
        foreach (var (startSample, samples, leftGain, rightGain) in rendered)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                left[startSample + i] += samples[i] * leftGain;
                right[startSample + i] += samples[i] * rightGain;
            }
        }

        return (ToFloatArray(left), ToFloatArray(right));
    }

    /// <summary>
    /// Stereo counterpart of <see cref="MixTracks"/>. The peak-normalization
    /// scale is computed once from the combined peak across BOTH channels and
    /// applied identically to both — normalizing channels independently would
    /// shift the stereo image.
    /// </summary>
    public static (float[] Left, float[] Right) MixTracksStereo(
        IReadOnlyList<(float[] Left, float[] Right)> trackBuffers)
    {
        var totalLength = 0;
        foreach (var (left, right) in trackBuffers)
            totalLength = Math.Max(totalLength, Math.Max(left.Length, right.Length));

        var mixedLeft = new double[totalLength];
        var mixedRight = new double[totalLength];
        foreach (var (left, right) in trackBuffers)
        {
            for (var i = 0; i < left.Length; i++)
                mixedLeft[i] += left[i];
            for (var i = 0; i < right.Length; i++)
                mixedRight[i] += right[i];
        }

        // Only scale down when the mix would otherwise clip, same policy as
        // the mono path (no dynamic range compression).
        var peak = 0.0;
        foreach (var sample in mixedLeft)
            peak = Math.Max(peak, Math.Abs(sample));
        foreach (var sample in mixedRight)
            peak = Math.Max(peak, Math.Abs(sample));

        var scale = peak > 1.0 ? 1.0 / peak : 1.0;

        var resultLeft = new float[totalLength];
        var resultRight = new float[totalLength];
        for (var i = 0; i < totalLength; i++)
        {
            resultLeft[i] = (float)(mixedLeft[i] * scale);
            resultRight[i] = (float)(mixedRight[i] * scale);
        }

        return (resultLeft, resultRight);
    }

    /// <summary>
    /// Constant-power pan gains via the square-root law:
    /// Left = √((1−pan)/2), Right = √((1+pan)/2), so Left² + Right² = 1 for
    /// every pan in [-1, 1] — no perceived volume dip at center. The sqrt law
    /// is chosen over the equivalent sin/cos law deliberately: IEEE 754
    /// guarantees Math.Sqrt is correctly rounded (bit-identical across
    /// platforms) while Math.Sin/Math.Cos are not, so panning stays
    /// platform-deterministic without involving DeterministicMath.
    /// </summary>
    internal static (double Left, double Right) PanGains(double pan)
    {
        var clamped = Math.Clamp(pan, -1.0, 1.0);
        return (Math.Sqrt((1.0 - clamped) / 2.0), Math.Sqrt((1.0 + clamped) / 2.0));
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
