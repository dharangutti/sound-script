namespace SoundScript.Vocal;

/// <summary>Offline speech synthesizer that writes 44.1 kHz mono WAV files.</summary>
public interface IVocalEngine
{
    string Name { get; }

    void Synthesize(string text, string outputWavPath, VocalEngineOptions options);
}
