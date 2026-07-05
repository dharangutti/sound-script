// UNDER DEVELOPMENT — v1 prototype
using System.Text;

namespace SoundScript.Wave.Io;

/// <summary>
/// Writes deterministic 16-bit PCM mono WAV files at 44.1kHz, byte-level,
/// with no third-party audio library. Stereo (using NoteEvent.Timbre.Pan)
/// is a v2 concern — this writer only ever emits a single channel.
/// </summary>
public static class WavWriter
{
    public const int SampleRate = 44_100;
    private const short BitsPerSample = 16;
    private const short Channels = 1;

    public static void Write(string outputPath, float[] samples) =>
        Write(outputPath, samples, SampleRate);

    public static void Write(string outputPath, float[] samples, int sampleRate)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var stream = File.Create(outputPath);
        WriteTo(stream, samples, sampleRate);
    }

    public static void WriteTo(Stream stream, float[] samples, int sampleRate)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        var byteRate = sampleRate * Channels * BitsPerSample / 8;
        var blockAlign = (short)(Channels * BitsPerSample / 8);
        var dataSize = samples.Length * blockAlign;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write(Channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            writer.Write((short)Math.Round(clamped * short.MaxValue));
        }
    }
}
