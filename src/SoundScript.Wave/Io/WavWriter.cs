// UNDER DEVELOPMENT — v2
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

        foreach (var sample in samples)
            WriteSample(writer, sample);
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

        for (var i = 0; i < left.Length; i++)
        {
            WriteSample(writer, left[i]);
            WriteSample(writer, right[i]);
        }
    }

    private static FileStream CreateFile(string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return File.Create(outputPath);
    }

    private static void WriteHeader(BinaryWriter writer, short channels, int sampleRate, int frameCount)
    {
        var byteRate = sampleRate * channels * BitsPerSample / 8;
        var blockAlign = (short)(channels * BitsPerSample / 8);
        var dataSize = frameCount * blockAlign;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
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
        writer.Write(dataSize);
    }

    private static void WriteSample(BinaryWriter writer, float sample)
    {
        var clamped = Math.Clamp(sample, -1.0f, 1.0f);
        writer.Write((short)Math.Round(clamped * short.MaxValue));
    }
}
