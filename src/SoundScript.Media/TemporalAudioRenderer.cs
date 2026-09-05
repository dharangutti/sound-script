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
