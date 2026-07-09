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
/// This is a parallel rail alongside SoundScript.Midi, selected by the
/// <c>.ssw</c> file extension, reusing the existing grammar/tokenizer/parser/
/// AST without introducing a new language. It references SoundScript.Core
/// only, so it can be added or removed without touching SoundScript.Midi,
/// SoundScript.Timbre, or the parser.
///
/// Determinism: identical input produces byte-identical WAV output —
/// no randomness, no wall-clock dependence, no non-deterministic
/// floating-point summation ordering (see Mixing.Mixer).
///
/// Consumers: the CLI's <c>soundscript wave &lt;script.ss|.ssw&gt; [out.wav]
/// [--stereo]</c> subcommand and the Blazor Playground's wave rail both parse
/// with SoundScript.Parser and hand the resulting ProgramNode to
/// <see cref="Render"/>/<see cref="RenderStereoToBytes"/>. This project itself
/// still references SoundScript.Core only — parsing is kept on the caller's
/// side so SoundScript.Wave's single dependency stays Core. See
/// SoundScript.Tests for the internal verification harness and
/// <c>examples/full-song-wave.ss</c> for an end-to-end four-part sample.
/// </summary>
public static class WaveRenderer
{
    public static byte[] RenderToBytes(ProgramNode program)
    {
        using var stream = new MemoryStream();
        RenderTo(program, stream);
        return stream.ToArray();
    }

    public static void Render(ProgramNode program, string outputWavPath)
    {
        var mixed = MixProgram(program);
        WavWriter.Write(outputWavPath, mixed);
    }

    public static void RenderTo(ProgramNode program, Stream destination)
    {
        var mixed = MixProgram(program);
        WavWriter.WriteTo(destination, mixed, WavWriter.SampleRate);
    }

    /// <summary>
    /// Stereo (interleaved L/R 16-bit PCM) counterpart of
    /// <see cref="RenderToBytes"/>. Because no .ss/.ssw grammar directive for
    /// pan exists (adding one would require modifying SoundScript.Core or
    /// SoundScript.Parser, which the safeguards forbid), the adapter still
    /// assigns Pan = 0.0 to every note — a parsed program renders dead-center,
    /// as a mono image duplicated to both channels. The pan plumbing below the
    /// adapter (Mixer → stereo WAV writer) is fully live, so direct API
    /// callers constructing NoteEvents with non-zero Pan get true stereo
    /// today; see Mixer.RenderTrackStereo for the full scope rationale.
    /// </summary>
    public static byte[] RenderStereoToBytes(ProgramNode program)
    {
        using var stream = new MemoryStream();
        RenderStereoTo(program, stream);
        return stream.ToArray();
    }

    public static void RenderStereo(ProgramNode program, string outputWavPath)
    {
        var (left, right) = MixProgramStereo(program);
        WavWriter.WriteStereo(outputWavPath, left, right);
    }

    public static void RenderStereoTo(ProgramNode program, Stream destination)
    {
        var (left, right) = MixProgramStereo(program);
        WavWriter.WriteStereoTo(destination, left, right, WavWriter.SampleRate);
    }

    /// <summary>
    /// SHA-256 of <see cref="RenderToBytes"/>, hex-encoded. Mirrors
    /// SoundScript.Timbre.OfflineRenderer.RenderSha256 — the checksum
    /// determinism suite hashes the render instead of asserting on raw WAV
    /// bytes directly, so a mismatch reports a 64-char digest rather than
    /// dumping the buffer (see WaveDeterminismTests).
    /// </summary>
    public static string RenderSha256(ProgramNode program) =>
        Convert.ToHexString(SHA256.HashData(RenderToBytes(program)));

    /// <summary>Stereo counterpart of <see cref="RenderSha256"/>.</summary>
    public static string RenderStereoSha256(ProgramNode program) =>
        Convert.ToHexString(SHA256.HashData(RenderStereoToBytes(program)));

    // v3: the master effects chain runs post-mix, as the final stage before
    // the WAV writer — master-only by design (see MasterEffectChain for the
    // full rationale; per-track routing stays in the parking lot). Programs
    // without effect directives take the identical pre-v3 path: an empty
    // chain returns the mixed buffer untouched.
    private static float[] MixProgram(ProgramNode program)
    {
        var tracks = AstToNoteEventAdapter.Convert(program);

        var trackBuffers = new List<float[]>(tracks.Count);
        foreach (var notes in tracks.Values)
        {
            if (notes.Count > 0)
                trackBuffers.Add(Mixer.RenderTrack(notes, WavWriter.SampleRate));
        }

        var mixed = Mixer.MixTracks(trackBuffers);
        return MasterEffectChain.Apply(mixed, EffectSettingsFactory.FromProgram(program), WavWriter.SampleRate);
    }

    private static (float[] Left, float[] Right) MixProgramStereo(ProgramNode program)
    {
        var tracks = AstToNoteEventAdapter.Convert(program);

        var trackBuffers = new List<(float[] Left, float[] Right)>(tracks.Count);
        foreach (var notes in tracks.Values)
        {
            if (notes.Count > 0)
                trackBuffers.Add(Mixer.RenderTrackStereo(notes, WavWriter.SampleRate));
        }

        var (left, right) = Mixer.MixTracksStereo(trackBuffers);
        return MasterEffectChain.ApplyStereo(left, right, EffectSettingsFactory.FromProgram(program), WavWriter.SampleRate);
    }
}
