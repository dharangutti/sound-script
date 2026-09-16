using SoundScript.Wave;
using SoundScript.Wave.Io;

namespace SoundScript.Transcription;

public static class TranscriptionPlayback
{
    public static byte[] Render(string source)
    {
        var wave = WaveRenderer.RenderToBytes(SoundScriptOutput.Parse(source));
        EnsureAudible(wave);
        return wave;
    }
    public static void EnsureAudible(byte[] wave)
    {
        using var stream = new MemoryStream(wave);
        var samples = WavReader.ReadMono(stream);
        // Same absolute activity floor as analysis; avoid treating dither/roundoff as music.
        if (samples.Length == 0 || !samples.Any(x => Math.Abs(x) >= .003f))
            throw new InvalidDataException("Generated playback contains no audible signal. Add usable notes or percussion hits before playback or export.");
    }
}
