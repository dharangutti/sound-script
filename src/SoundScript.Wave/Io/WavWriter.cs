// UNDER DEVELOPMENT — v2
using System.Buffers.Binary;
using System.Text;

namespace SoundScript.Wave.Io;

/// <summary>
/// Writes deterministic 16-bit PCM WAV files at 44.1kHz, byte-level, with no
/// third-party audio library. Mono (v1) and interleaved-stereo (v2) paths
/// live side by side: the mono methods are byte-for-byte unchanged from v1 —
/// existing callers keep producing identical files — and the stereo methods
/// take separate left/right buffers and interleave L/R per frame.
/// </summary>
public static class WavWriter
{
    public const int SampleRate = 44_100;
    private const short BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;
    private const short MonoChannels = 1;
    private const short StereoChannels = 2;

    public static void Write(string outputPath, float[] samples) =>
        Write(outputPath, samples, SampleRate);

    public static void Write(string outputPath, float[] samples, int sampleRate)
    {
        using var stream = CreateFile(outputPath);
        WriteTo(stream, samples, sampleRate);
    }

    public static void WriteTo(Stream stream, float[] samples, int sampleRate)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        WriteHeader(writer, MonoChannels, sampleRate, frameCount: samples.Length);

        var buffer = new byte[samples.Length * BytesPerSample];
        for (var i = 0; i < samples.Length; i++)
            WriteSampleTo(buffer, i * BytesPerSample, samples[i]);

        writer.Write(buffer);
    }

    public static void WriteStereo(string outputPath, float[] left, float[] right) =>
        WriteStereo(outputPath, left, right, SampleRate);

    public static void WriteStereo(string outputPath, float[] left, float[] right, int sampleRate)
    {
        using var stream = CreateFile(outputPath);
        WriteStereoTo(stream, left, right, sampleRate);
    }

    public static void WriteStereoTo(Stream stream, float[] left, float[] right, int sampleRate)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException(
                $"Stereo channels must be equal length (left: {left.Length}, right: {right.Length}).",
                nameof(right));
        }

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        WriteHeader(writer, StereoChannels, sampleRate, frameCount: left.Length);

        var buffer = new byte[left.Length * StereoChannels * BytesPerSample];
        for (var i = 0; i < left.Length; i++)
        {
            var frameOffset = i * StereoChannels * BytesPerSample;
            WriteSampleTo(buffer, frameOffset, left[i]);
            WriteSampleTo(buffer, frameOffset + BytesPerSample, right[i]);
        }

        writer.Write(buffer);
    }

    private static FileStream CreateFile(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return File.Create(outputPath);
    }

    internal static void WriteHeader(BinaryWriter writer, short channels, int sampleRate, int frameCount)
    {
        var byteRate = sampleRate * channels * BitsPerSample / 8;
        var blockAlign = (short)(channels * BitsPerSample / 8);

        // Widen to long before multiplying: frameCount * blockAlign can
        // exceed Int32.MaxValue for long enough renders, and the classic
        // RIFF/WAV format's size fields are hard-coded to 32 bits on disk —
        // there's no wider field to widen into, so a render that doesn't fit
        // must fail loudly instead of silently wrapping into a corrupt header.
        var dataSize = (long)frameCount * blockAlign;
        if (dataSize > int.MaxValue - 36)
        {
            throw new InvalidOperationException(
                $"WAV render is too long to fit in a classic RIFF/WAV header: {frameCount} frames at " +
                $"{blockAlign} bytes/frame would need a {dataSize}-byte data chunk, but the format's " +
                "32-bit size fields cap this at just under 2 GiB. Render a shorter buffer.");
        }

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write((int)(36 + dataSize));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write((int)dataSize);
    }

    private static void WriteSampleTo(byte[] buffer, int byteOffset, float sample)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        var pcm = (short)Math.Round(clamped * short.MaxValue);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(byteOffset, BytesPerSample), pcm);
    }
}
