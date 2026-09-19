using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoundScript.Labs.IR;
using SoundScript.Wave.Io;

namespace SoundScript.Labs.Output;

public static class ResultExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static byte[] SerializeIr(ExperimentIr ir) { ir.Validate(); return JsonSerializer.SerializeToUtf8Bytes(ir, JsonOptions); }

    public static SortedDictionary<string, byte[]> Export(ExperimentRun run)
    {
        var ir = run.Experiment;
        run.Buffer.ValidateFor(ir);
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var samples = run.Buffer.Samples;
        byte[] floats = new byte[samples.Length * 4];
        for (int i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(floats.AsSpan(i * 4, 4), samples[i]);
        if (ir.Exports.HasFlag(ExportKind.Float32)) files.Add("signal.f32", floats);
        if ((ir.Exports & (ExportKind.Pcm | ExportKind.Wav)) != 0)
        {
            using var stream = new MemoryStream();
            WavWriter.WriteTo(stream, samples.ToArray(), ir.Signal.SampleRate);
            var wav = stream.ToArray();
            if (ir.Exports.HasFlag(ExportKind.Wav)) files.Add("signal.wav", wav);
            if (ir.Exports.HasFlag(ExportKind.Pcm)) files.Add("signal.pcm", wav[44..]);
        }
        // Traceability metadata accompanies every requested output, even export wav alone.
        files.Add("result.json", JsonSerializer.SerializeToUtf8Bytes(new
        {
            status = "experimental-simulation-only",
            engine_version = ExperimentRunner.EngineVersion,
            wave_assembly_version = typeof(WavWriter).Assembly.GetName().Version?.ToString(),
            backend = run.Backend,
            source_sha256 = run.SourceSha256,
            ir_sha256 = Hash(SerializeIr(ir)),
            float32_sha256 = Hash(floats),
            experiment = ir,
            sample_rate = ir.Signal.SampleRate,
            sample_count = samples.Length,
            channels = 1,
            start_time_seconds = 0,
            duration_seconds = samples.Length / (double)ir.Signal.SampleRate,
            analysis = run.Analysis,
            outputs = files.Select(pair => new { file = pair.Key, bytes = pair.Value.Length, sha256 = Hash(pair.Value) }).ToArray(),
            diagnostics = new[]
            {
                "Synthetic stimulus only; no captured response, resonance measurement or hardware validation.",
                "FFT uses rectangular window; bin estimates have finite resolution and spectral leakage.",
                ir.Signal.Waveform == Waveform.Square
                    ? "Ideal sampled square wave is not band-limited; harmonics may alias."
                    : "Frequencies are constrained below Nyquist."
            }
        }, JsonOptions));
        return files;
    }
}
