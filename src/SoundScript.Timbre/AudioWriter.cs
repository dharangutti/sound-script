using System.Text;
using OggVorbisEncoder;

namespace SoundScript.Timbre;

/// <summary>
/// Writes deterministic PCM audio to WAV or OGG files. No randomness, no
/// platform-dependent floating-point surprises beyond IEEE-754 single output.
/// </summary>
public static class AudioWriter
{
    /// <summary>Fixed Ogg stream serial for deterministic page headers.</summary>
    private const int OggStreamSerial = 0x53534D49; // "SSMI"

    /// <summary>Writes samples to a file, choosing the format from the extension.</summary>
    public static void Write(string outputPath, float[] samples, int sampleRate)
    {
        var extension = Path.GetExtension(outputPath).ToLowerInvariant();
        switch (extension)
        {
            case ".wav":
                WriteWav(outputPath, samples, sampleRate);
                break;
            case ".ogg":
                WriteOgg(outputPath, samples, sampleRate);
                break;
            default:
                throw new NotSupportedException($"Unsupported audio extension '{extension}'. Use .wav or .ogg.");
        }
    }

    /// <summary>Writes 16-bit PCM mono WAV.</summary>
    public static void WriteWav(string outputPath, float[] samples, int sampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        var bitsPerSample = 16;
        var channels = 1;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        var dataSize = samples.Length * blockAlign;

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1.0f, 1.0f);
            writer.Write((short)Math.Round(clamped * 32767.0));
        }
    }

    /// <summary>Writes mono Ogg Vorbis using a fixed quality and stream serial.</summary>
    public static void WriteOgg(string outputPath, float[] samples, int sampleRate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
        File.WriteAllBytes(outputPath, EncodeOgg(samples, sampleRate));
    }

    /// <summary>Encodes mono float PCM into deterministic Ogg Vorbis bytes.</summary>
    public static byte[] EncodeOgg(float[] samples, int sampleRate)
    {
        const int channels = 1;
        const float quality = 0.4f;
        const int writeBufferSize = 512;

        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);
        var comments = new Comments();
        comments.AddTag("ENCODER", "SoundScript.Timbre");

        var oggStream = new OggStream(OggStreamSerial);
        oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

        using var output = new MemoryStream();
        FlushPages(oggStream, output, force: true);

        var processingState = ProcessingState.Create(info);
        var buffer = new[] { new float[writeBufferSize] };
        var read = 0;

        while (read < samples.Length)
        {
            var count = Math.Min(writeBufferSize, samples.Length - read);
            Array.Copy(samples, read, buffer[0], 0, count);
            if (count < writeBufferSize)
                Array.Clear(buffer[0], count, writeBufferSize - count);

            processingState.WriteData(buffer, count, 0);
            DrainPackets(processingState, oggStream, output);
            read += count;
        }

        processingState.WriteEndOfStream();
        DrainPackets(processingState, oggStream, output);
        FlushPages(oggStream, output, force: true);

        return output.ToArray();
    }

    private static void DrainPackets(ProcessingState processingState, OggStream oggStream, MemoryStream output)
    {
        while (processingState.PacketOut(out var packet))
        {
            oggStream.PacketIn(packet);
            FlushPages(oggStream, output, force: false);
        }
    }

    private static void FlushPages(OggStream oggStream, MemoryStream output, bool force)
    {
        while (oggStream.PageOut(out var page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }
}
