// UNDER DEVELOPMENT — v1 prototype
using SoundScript.Core.Ast;
using SoundScript.Wave.Adapter;
using SoundScript.Wave.Io;
using SoundScript.Wave.Mixing;

namespace SoundScript.Wave;

/// <summary>
/// SoundScript.Wave — v1 prototype. Renders directly from the shared AST to
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
/// Backend only: this class (and the whole module) is not wired into
/// SoundScript.Cli or SoundScript.Playground. Callers parse a <c>.ssw</c>
/// file with the existing SoundScript.Parser.ProgramLoader themselves (kept
/// out of this project so SoundScript.Wave's only dependency stays
/// SoundScript.Core) and hand the resulting ProgramNode to <see cref="Render"/>.
/// See SoundScript.Tests for a minimal internal verification harness.
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

    private static float[] MixProgram(ProgramNode program)
    {
        var tracks = AstToNoteEventAdapter.Convert(program);

        var trackBuffers = new List<float[]>(tracks.Count);
        foreach (var notes in tracks.Values)
        {
            if (notes.Count > 0)
                trackBuffers.Add(Mixer.RenderTrack(notes, WavWriter.SampleRate));
        }

        return Mixer.MixTracks(trackBuffers);
    }
}
