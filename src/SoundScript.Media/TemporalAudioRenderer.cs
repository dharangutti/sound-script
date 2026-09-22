using SoundScript.Core.Ast;
using SoundScript.Wave;
using SoundScript.Wave.Io;

namespace SoundScript.Media;

/// <summary>
/// Produces the canonical PCM rail for temporal media. The same deterministic
/// SoundScript.Wave bytes are used by the Playground preview, browser WebM
/// encoder, and CLI encoder; container codecs remain downstream adapters.
/// </summary>
public static class TemporalAudioRenderer
{
    /// <summary>Renders the complete PCM rail and pads it with silence to cover a minimum media duration.</summary>
    /// <remarks>Unlike the fixed-duration video overload, this never truncates audio. Explicit options preserve
    /// the host's filesystem policy; missing samples are not silently skipped by default.</remarks>
    public static byte[] RenderCompleteWavBytes(ProgramNode program, TimeSpan minimumDuration, WaveRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (minimumDuration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(minimumDuration));
        var bytes = WaveRenderer.RenderToBytes(program, options);
        using var input = new MemoryStream(bytes, writable: false);
        var samples = WavReader.ReadMono(input);
        var count = checked((int)Math.Ceiling(minimumDuration.TotalSeconds * WavWriter.SampleRate));
        if (samples.Length >= count) return bytes;
        // Preserve the original quantized PCM bytes exactly when extending the rail.
        var padded = new byte[checked(44 + count * 2)];
        bytes.CopyTo(padded, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(padded.AsSpan(4), padded.Length - 8);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(padded.AsSpan(40), count * 2);
        return padded;
    }

    /// <summary>Uses the PCM adapter's clock, including Wave-only programs.</summary>
    public static SoundScript.Core.TempoAutomationMap BuildTempoMap(ProgramNode program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return SoundScript.Wave.Adapter.AstToNoteEventAdapter.Adapt(program).TempoMap;
    }

    public static byte[] RenderToWavBytes(ProgramNode program, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));

        // Browser and CLI must have the same asset policy. The Playground has
        // no script-relative filesystem, so this profile skips unavailable
        // external samples everywhere instead of letting an export silently
        // diverge by host.
        var renderedWav = WaveRenderer.RenderToBytes(program, new WaveRenderOptions
        {
            SkipMissingSamples = true,
        });

        using var renderedStream = new MemoryStream(renderedWav, writable: false);
        var rendered = WavReader.ReadMono(renderedStream);
        var expectedSamples = checked((int)Math.Ceiling(duration.TotalSeconds * WavWriter.SampleRate));
        var fitted = new float[expectedSamples];
        Array.Copy(rendered, fitted, Math.Min(rendered.Length, fitted.Length));

        using var output = new MemoryStream();
        WavWriter.WriteTo(output, fitted, WavWriter.SampleRate);
        return output.ToArray();
    }
}
