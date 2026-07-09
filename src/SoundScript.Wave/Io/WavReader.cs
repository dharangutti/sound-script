using System.Buffers.Binary;
using System.Text;

namespace SoundScript.Wave.Io;

/// <summary>
/// Reads 16-bit PCM WAV files for vocal stem mixing (V8). Output is always
/// mono float samples at <see cref="WavWriter.SampleRate"/> for the wave mixer.
/// </summary>
public static class WavReader
{
    public static float[] ReadMono(string path)
    {
        using var stream = File.OpenRead(path);
        return ReadMono(stream);
    }

    public static float[] ReadMono(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF WAV file.");

        _ = reader.ReadInt32(); // chunk size
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        short channels = 0;
        var sampleRate = 0;
        short bitsPerSample = 0;
        byte[]? data = null;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var chunkSize = reader.ReadInt32();

            switch (chunkId)
            {
                case "fmt ":
                    _ = reader.ReadInt16(); // audio format — PCM only
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    _ = reader.ReadInt32(); // byte rate
                    _ = reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();
                    if (chunkSize > 16)
                        reader.BaseStream.Seek(chunkSize - 16, SeekOrigin.Current);
                    break;
                case "data":
                    data = reader.ReadBytes(chunkSize);
                    break;
                default:
                    reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                    break;
            }
        }

        if (data is null)
            throw new InvalidDataException("WAV file has no data chunk.");

        if (bitsPerSample != 16)
            throw new NotSupportedException($"Only 16-bit PCM WAV is supported (got {bitsPerSample}-bit).");

        if (channels is not (1 or 2))
            throw new NotSupportedException($"Only mono or stereo WAV is supported (got {channels} channels).");

        var frameCount = data.Length / (bitsPerSample / 8) / channels;
        var pcm = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            double sum = 0;
            for (var ch = 0; ch < channels; ch++)
            {
                var offset = (i * channels + ch) * 2;
                var sample = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));
                sum += sample / (double)short.MaxValue;
            }

            pcm[i] = (float)(sum / channels);
        }

        if (sampleRate == WavWriter.SampleRate)
            return pcm;

        return ResampleLinear(pcm, sampleRate, WavWriter.SampleRate);
    }

    private static float[] ResampleLinear(float[] input, int sourceRate, int targetRate)
    {
        if (sourceRate <= 0 || targetRate <= 0)
            throw new InvalidDataException("Invalid WAV sample rate.");

        var outputLength = (int)Math.Max(1, Math.Round(input.Length * (double)targetRate / sourceRate));
        var output = new float[outputLength];
        var ratio = (double)sourceRate / targetRate;

        for (var i = 0; i < outputLength; i++)
        {
            var src = i * ratio;
            var index = (int)src;
            var frac = src - index;
            if (index >= input.Length - 1)
            {
                output[i] = input[^1];
                continue;
            }

            output[i] = (float)(input[index] * (1 - frac) + input[index + 1] * frac);
        }

        return output;
    }
}
