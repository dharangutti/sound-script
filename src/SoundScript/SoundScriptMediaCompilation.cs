using SoundScript.Core.Ast;
using SoundScript.Media;
using SoundScript.Visual;
using SoundScript.Wave;
using SoundScript.Wave.Io;

namespace SoundScript;

/// <summary>A reusable media program backed by the existing visual timeline and Wave renderer.</summary>
/// <remarks>No host playback state is stored. Audio is rendered once on first audio/duration access;
/// external assets must remain stable until then. Returned audio buffers are defensive copies.</remarks>
public sealed class SoundScriptMediaCompilation
{
    private readonly Lazy<(byte[] Bytes, TimeSpan Duration)> audio;

    internal SoundScriptMediaCompilation(ProgramNode program, WaveRenderOptions options)
    {
        Timeline = VisualInterpreter.Interpret(program);
        audio = new(() =>
        {
            var bytes = TemporalAudioRenderer.RenderCompleteWavBytes(program, Timeline.Duration, options);
            using var stream = new MemoryStream(bytes, writable: false);
            var samples = WavReader.ReadMono(stream);
            return (bytes, TimeSpan.FromSeconds((double)samples.Length / WavWriter.SampleRate));
        });
    }

    /// <summary>The existing immutable visual timeline, including declared audio sync markers.</summary>
    public VisualTimeline Timeline { get; }

    /// <summary>The complete audio duration, padded to cover the visual timeline at PCM sample precision.</summary>
    public TimeSpan Duration => audio.Value.Duration;

    /// <summary>Returns deterministic mono PCM WAV, preserving the audio tail and padding visual-only time with silence.</summary>
    /// <exception cref="FileNotFoundException">A required external audio asset is missing.</exception>
    public byte[] RenderAudio() => (byte[])audio.Value.Bytes.Clone();

    /// <summary>Projects the existing timeline at host-supplied media time into a typed scene.</summary>
    /// <remarks>Intervals are [start,end). At or after the final visual end the scene is empty; time is never clamped.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Time is negative.</exception>
    public TemporalVisualScene SceneAt(TimeSpan time) => TemporalVisualSceneBuilder.Build(Timeline.StateAt(time));
}
