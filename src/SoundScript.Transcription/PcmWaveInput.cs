using System.Buffers.Binary;

namespace SoundScript.Transcription;

/// <summary>Strict RIFF PCM/IEEE-float decoder; unsupported encodings can use the desktop FFmpeg adapter.</summary>
public sealed class PcmWaveInput : ITranscriptionInputAdapter<byte[]>
{
    public Task<AnalysisAudio> DecodeAsync(byte[] input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Decode(input));
    }

    public static AnalysisAudio Decode(byte[] bytes, MediaExcerpt? excerpt = null, Action<MediaInfo>? inspected = null)
    {
        var span = bytes.AsSpan();
        if (span.Length < 12 || !span[..4].SequenceEqual("RIFF"u8) || !span.Slice(8,4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Expected a RIFF WAVE file.");
        long declared = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) + 8L;
        if (declared > span.Length || declared < 12) throw new InvalidDataException("Truncated RIFF file.");
        int format = 0, channels = 0, rate = 0, bits = 0, align = 0;
        ReadOnlySpan<byte> data = default;
        for (int offset = 12; offset + 8 <= declared;)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 4)..]);
            if (size > declared - offset - 8) throw new InvalidDataException("Truncated WAV chunk.");
            var chunk = span.Slice(offset + 8, (int)size);
            if (span.Slice(offset,4).SequenceEqual("fmt "u8))
            {
                if (size < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                format = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                align = BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
            }
            else if (span.Slice(offset,4).SequenceEqual("data"u8)) data = chunk;
            offset = checked(offset + 8 + (int)size + (int)(size % 2));
        }
        if (channels is < 1 or > 8 || rate is < 8000 or > 192000 || align != channels * (bits / 8) || align == 0)
            throw new InvalidDataException("Invalid WAV channel/sample format.");
        if (!(format == 1 && bits is 8 or 16 or 24 or 32 || format == 3 && bits == 32))
            throw new NotSupportedException("WAV encoding requires FFmpeg; native decoding supports PCM 8/16/24/32 and float32.");
        if (data.Length % align != 0) throw new InvalidDataException("Partial WAV sample frame.");
        int count = data.Length / align;
        double mediaDuration = (double)count / rate;
        inspected?.Invoke(new(mediaDuration, rate, channels));
        // Preserve empty PCM decoding for diagnostics and renderer verification.
        if (count == 0 && excerpt == null) return new([]);
        excerpt ??= new();
        double duration = excerpt.Validate(mediaDuration);
        int begin = (int)Math.Round(excerpt.StartSeconds * rate);
        count = Math.Min(count - begin, (int)Math.Round(duration * rate));
        data = data.Slice(begin * align, count * align);
        var mono = new float[count];
        for (int i = 0; i < count; i++)
        {
            double sum = 0;
            for (int c = 0; c < channels; c++)
            {
                var s = data[(i * align + c * bits / 8)..];
                double value = format == 3 ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(s)) : bits switch
                {
                    8 => (s[0] - 128) / 128.0,
                    16 => BinaryPrimitives.ReadInt16LittleEndian(s) / 32768.0,
                    24 => ((s[0] | s[1] << 8 | s[2] << 16) << 8 >> 8) / 8388608.0,
                    _ => BinaryPrimitives.ReadInt32LittleEndian(s) / 2147483648.0
                };
                if (!double.IsFinite(value) || Math.Abs(value) > 1.0001) throw new InvalidDataException("Invalid floating-point WAV sample.");
                sum += value;
            }
            mono[i] = (float)(sum / channels);
        }
        return Normalize(mono, rate);
    }

    public static AnalysisAudio Normalize(float[] samples, int sampleRate)
    {
        if (sampleRate is < 8000 or > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (samples.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1.0001f)) throw new InvalidDataException("Invalid PCM sample.");
        if (samples.Length / (double)sampleRate > AnalysisAudio.MaximumSeconds) throw new InvalidDataException("Audio exceeds the 120-second analysis limit.");
        if (sampleRate == AnalysisAudio.SampleRate) return new(samples);
        // Windowed-sinc resampling with a low-pass cutoff before decimation.
        double ratio = sampleRate / (double)AnalysisAudio.SampleRate;
        double cutoff = Math.Min(1, 1 / ratio) * .94;
        var output = new float[(int)Math.Round(samples.Length / ratio)];
        int radius = (int)Math.Ceiling(16 / cutoff);
        for (int i = 0; i < output.Length; i++)
        {
            double position = i * ratio, sum = 0, weight = 0;
            int center = (int)position;
            for (int j = Math.Max(0, center - radius); j <= Math.Min(samples.Length - 1, center + radius); j++)
            {
                double x = j - position;
                double sinc = Math.Abs(x) < 1e-10 ? cutoff : Math.Sin(Math.PI * cutoff * x) / (Math.PI * x);
                double w = sinc * (.5 + .5 * Math.Cos(Math.PI * x / (radius + 1)));
                sum += samples[j] * w; weight += w;
            }
            output[i] = (float)Math.Clamp(weight == 0 ? 0 : sum / weight, -1, 1);
        }
        return new(output);
    }
}
