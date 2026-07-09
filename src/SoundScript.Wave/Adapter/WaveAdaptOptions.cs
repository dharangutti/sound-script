namespace SoundScript.Wave.Adapter;

/// <summary>Adapter toggles for wave rendering (V8 vocal stems).</summary>
public sealed class WaveAdaptOptions
{
    /// <summary>
    /// When true, <c>speak</c> nodes without <c>sample=</c> advance the beat
    /// cursor but do not emit synthetic phoneme notes — used when pre-rendered
    /// stem overlays (<c>--tts-dir</c> / <c>--offline-tts</c>) supply the vocal.
    /// </summary>
    public bool SuppressSyntheticSpeak { get; init; }
}
