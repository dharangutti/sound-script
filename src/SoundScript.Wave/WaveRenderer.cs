// UNDER DEVELOPMENT — v3
using System.Security.Cryptography;
using SoundScript.Core.Ast;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Effects;
using SoundScript.Wave.Io;
using SoundScript.Wave.Mixing;

namespace SoundScript.Wave;

/// <summary>
/// SoundScript.Wave — v2. Renders directly from the shared AST to
/// raw WAV audio, with no MIDI step at any point:
///
/// <code>
/// .ssw file -&gt; (existing) Tokenizer -&gt; (existing) Parser -&gt; (existing) AST
///                                          |
///                                          v
///                                SoundScript.Wave (this module)
///                                          |
///                                          v
///                                     output.wav
/// </code>
///
/// V8 adds external vocal stem mixing via <c>sample</c>, <c>speak sample=</c>,
/// and CLI overlays (<c>--vocal</c>, <c>--tts-dir</c>).
/// </summary>
public static class WaveRenderer
{
    public static byte[] RenderToBytes(ProgramNode program, WaveRenderOptions? options = null)
    {
        using var stream = new MemoryStream();
        RenderTo(program, stream, options);
        return stream.ToArray();
    }

    public static void Render(ProgramNode program, string outputWavPath, WaveRenderOptions? options = null)
    {
        var mixed = MixProgram(program, options);
        WavWriter.Write(outputWavPath, mixed);
    }

    public static void RenderTo(ProgramNode program, Stream destination, WaveRenderOptions? options = null)
    {
        var mixed = MixProgram(program, options);
        WavWriter.WriteTo(destination, mixed, WavWriter.SampleRate);
    }

    public static byte[] RenderStereoToBytes(ProgramNode program, WaveRenderOptions? options = null)
    {
        using var stream = new MemoryStream();
        RenderStereoTo(program, stream, options);
        return stream.ToArray();
    }

    public static void RenderStereo(ProgramNode program, string outputWavPath, WaveRenderOptions? options = null)
    {
        var (left, right) = MixProgramStereo(program, options);
        WavWriter.WriteStereo(outputWavPath, left, right);
    }

    public static void RenderStereoTo(ProgramNode program, Stream destination, WaveRenderOptions? options = null)
    {
        var (left, right) = MixProgramStereo(program, options);
        WavWriter.WriteStereoTo(destination, left, right, WavWriter.SampleRate);
    }

    public static string RenderSha256(ProgramNode program, WaveRenderOptions? options = null) =>
        Convert.ToHexString(SHA256.HashData(RenderToBytes(program, options)));

    public static string RenderStereoSha256(ProgramNode program, WaveRenderOptions? options = null) =>
        Convert.ToHexString(SHA256.HashData(RenderStereoToBytes(program, options)));

    private static float[] MixProgram(ProgramNode program, WaveRenderOptions? options)
    {
        var adaptOptions = BuildAdaptOptions(options);
        var adapted = AstToNoteEventAdapter.Adapt(program, adaptOptions);
        var trackBuffers = RenderTrackBuffers(adapted.Tracks);
        var mixed = Mixer.SumTracksRaw(trackBuffers);
        mixed = ApplyOverlays(mixed, adapted.SampleOverlays, options);
        if (options?.AdditionalSampleOverlays is not null)
            mixed = ApplyOverlays(mixed, options.AdditionalSampleOverlays, options);
        mixed = ApplyExternalOverlays(mixed, options);
        var finalized = Mixer.FinalizeMix(mixed);
        return MasterEffectChain.Apply(finalized, EffectSettingsFactory.FromProgram(program), WavWriter.SampleRate);
    }

    private static (float[] Left, float[] Right) MixProgramStereo(ProgramNode program, WaveRenderOptions? options)
    {
        var adaptOptions = BuildAdaptOptions(options);
        var adapted = AstToNoteEventAdapter.Adapt(program, adaptOptions);
        var trackBuffers = RenderTrackBuffersStereo(adapted.Tracks);
        var (leftMixed, rightMixed) = Mixer.SumTracksStereoRaw(trackBuffers);
        leftMixed = ApplyOverlays(leftMixed, adapted.SampleOverlays, options);
        rightMixed = ApplyOverlays(rightMixed, adapted.SampleOverlays, options);
        if (options?.AdditionalSampleOverlays is not null)
        {
            leftMixed = ApplyOverlays(leftMixed, options.AdditionalSampleOverlays, options);
            rightMixed = ApplyOverlays(rightMixed, options.AdditionalSampleOverlays, options);
        }
        leftMixed = ApplyExternalOverlays(leftMixed, options);
        rightMixed = ApplyExternalOverlays(rightMixed, options);
        var (left, right) = Mixer.FinalizeMixStereo(leftMixed, rightMixed);
        return MasterEffectChain.ApplyStereo(left, right, EffectSettingsFactory.FromProgram(program), WavWriter.SampleRate);
    }

    private static List<float[]> RenderTrackBuffers(Dictionary<string, List<Model.NoteEvent>> tracks)
    {
        var trackBuffers = new List<float[]>(tracks.Count);
        foreach (var notes in tracks.Values)
        {
            if (notes.Count > 0)
                trackBuffers.Add(Mixer.RenderTrack(notes, WavWriter.SampleRate));
        }

        return trackBuffers;
    }

    private static List<(float[] Left, float[] Right)> RenderTrackBuffersStereo(
        Dictionary<string, List<Model.NoteEvent>> tracks)
    {
        var trackBuffers = new List<(float[] Left, float[] Right)>(tracks.Count);
        foreach (var notes in tracks.Values)
        {
            if (notes.Count > 0)
                trackBuffers.Add(Mixer.RenderTrackStereo(notes, WavWriter.SampleRate));
        }

        return trackBuffers;
    }

    private static double[] ApplyOverlays(
        double[] mixed,
        IReadOnlyList<SampleOverlayRequest> overlays,
        WaveRenderOptions? options)
    {
        foreach (var overlay in overlays)
        {
            var path = WavePathResolver.Resolve(options?.ScriptDirectory, overlay.RelativePath);
            if (options?.SkipMissingSamples == true && !File.Exists(path))
                continue;

            var samples = WavReader.ReadMono(path);
            var startSample = Math.Max(0, (int)Math.Round(overlay.StartTimeSeconds * WavWriter.SampleRate));
            mixed = Mixer.OverlayMono(mixed, samples, startSample, overlay.Gain);
        }

        return mixed;
    }

    private static double[] ApplyExternalOverlays(double[] mixed, WaveRenderOptions? options)
    {
        if (options?.ExternalOverlays is null)
            return mixed;

        foreach (var overlay in options.ExternalOverlays)
        {
            var startSample = Math.Max(0, (int)Math.Round(overlay.StartTimeSeconds * WavWriter.SampleRate));
            mixed = Mixer.OverlayMono(mixed, overlay.Samples, startSample, overlay.Gain);
        }

        return mixed;
    }

    private static WaveAdaptOptions? BuildAdaptOptions(WaveRenderOptions? options)
    {
        if (options?.SuppressSyntheticSpeak != true
            && (options?.AdditionalSampleOverlays is null || options.AdditionalSampleOverlays.Count == 0))
        {
            return null;
        }

        return new WaveAdaptOptions { SuppressSyntheticSpeak = true };
    }
}
