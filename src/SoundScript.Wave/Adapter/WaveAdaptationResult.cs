namespace SoundScript.Wave.Adapter;

using SoundScript.Core.Ast;

/// <summary>Request to mix an external WAV at a point in the render timeline (V8).</summary>
public sealed record SampleOverlayRequest(
    string RelativePath,
    double StartTimeSeconds,
    double Gain);

/// <summary>Result of adapting an AST for wave rendering — synthesized tracks plus optional stems.</summary>
public sealed record WaveAdaptationResult(
    Dictionary<string, List<Model.NoteEvent>> Tracks,
    IReadOnlyList<SampleOverlayRequest> SampleOverlays,
    IReadOnlyList<(SpeakNode Speak, double StartTimeSeconds)> SpeakTimings);
