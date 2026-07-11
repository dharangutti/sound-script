using SoundScript.Timbre;

namespace SoundScript.Vocal;

/// <summary>
/// A single word after per-word DSP rendering, carrying the PCM plus the plan and
/// metadata the continuous stitcher needs to smooth boundaries.
/// </summary>
public sealed record RenderedWord(float[] Pcm, DspTransformPlan Plan, string Word);
