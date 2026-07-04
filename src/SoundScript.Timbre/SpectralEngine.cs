namespace SoundScript.Timbre;

/// <summary>
/// Deterministic frame-based spectral synthesizer. Generates voiced formants,
/// noise layers, plosive bursts, and nasal resonance without randomness.
/// </summary>
public static class SpectralEngine
{
  private const double TwoPi = Math.PI * 2.0;

  /// <summary>Synthesizes PCM samples for an entire timeline.</summary>
  public static float[] Synthesize(TimbreTimeline timeline)
  {
    var samples = new float[timeline.SampleCount];
    var frameSampleCount = (int)Math.Round(timeline.SampleRate * timeline.FrameMs / 1000.0);
    var state = new SynthState();

    for (var frame = 0; frame < timeline.FrameCount; frame++)
    {
      var frameStartMs = frame * timeline.FrameMs;
      var active = FindActiveSegment(timeline.Segments, frameStartMs);
      var frameEnd = Math.Min(timeline.SampleCount, (frame + 1) * frameSampleCount);

      if (active is null)
      {
        AdvanceSilence(state, frameStartMs, frameEnd - frame * frameSampleCount, timeline.SampleRate, samples, frame * frameSampleCount);
        continue;
      }

      var profile = InterpolateProfile(active, frameStartMs);
      var amplitude = VelocityToAmplitude(active.Velocity);
      var frameLength = frameEnd - frame * frameSampleCount;
      SynthesizeFrame(
        state,
        active.PitchHz,
        profile,
        amplitude,
        frameStartMs,
        active.StartMs,
        active.DurationMs,
        frameLength,
        timeline.SampleRate,
        samples,
        frame * frameSampleCount);
    }

    Normalize(samples);
    return samples;
  }

  private static TimbreSegment? FindActiveSegment(IReadOnlyList<TimbreSegment> segments, double timeMs)
  {
    for (var i = segments.Count - 1; i >= 0; i--)
    {
      var segment = segments[i];
      if (timeMs >= segment.StartMs && timeMs < segment.StartMs + segment.DurationMs)
        return segment;
    }

    return null;
  }

  private static TimbreProfile InterpolateProfile(TimbreSegment segment, double timeMs)
  {
    var position = (timeMs - segment.StartMs) / Math.Max(segment.DurationMs, 1.0);
  var smooth = Math.Clamp(segment.Profile.Smoothness, 0, 1);
  var blend = smooth > 0 ? Math.Pow(position, 1.0 + smooth * 3.0) : position;
  var openness = segment.Profile.Openness * (0.6 + 0.4 * blend);

    return segment.Profile.With(
      formant1Hz: segment.Profile.Formant1Hz * (0.85 + 0.3 * openness),
      formant2Hz: segment.Profile.Formant2Hz * (0.9 + 0.2 * openness),
      formant3Hz: segment.Profile.Formant3Hz * (0.92 + 0.15 * openness));
  }

  private static void SynthesizeFrame(
    SynthState state,
    double pitchHz,
    TimbreProfile profile,
    double amplitude,
    double frameStartMs,
    double noteStartMs,
    double noteDurationMs,
    int frameLength,
    int sampleRate,
    float[] output,
    int outputOffset)
  {
    var phaseIncrement = pitchHz / sampleRate;
    var burstSamples = (int)Math.Round(profile.BurstMs * sampleRate / 1000.0);
    var noteElapsedMs = frameStartMs - noteStartMs;

    for (var i = 0; i < frameLength; i++)
    {
      var globalIndex = outputOffset + i;
      var t = (noteElapsedMs + i * 1000.0 / sampleRate) / Math.Max(noteDurationMs, 1.0);
      var envelope = NoteEnvelope(t, profile.Smoothness);
      var burst = BurstEnvelope(globalIndex - outputOffset, burstSamples, profile.BurstMs > 0);

      state.Phase += phaseIncrement;
      if (state.Phase >= 1.0)
        state.Phase -= 1.0;

      var source = Math.Sin(TwoPi * state.Phase);
      var voiced = FormantFilter(state, source, profile);
      var noise = DeterministicNoise(state.NoiseIndex++) * profile.Noise;
      var brightness = HighShelf(noise + voiced * (1.0 - profile.Noise), profile.Brightness);
      var nasal = NasalResonance(brightness, profile.Nasal, state);

      var sample = amplitude * envelope * (burst + (1.0 - profile.BurstMs / Math.Max(noteDurationMs, 1.0)) * nasal);
      output[globalIndex] += (float)sample;
    }
  }

  private static void AdvanceSilence(
    SynthState state,
    double frameStartMs,
    int frameLength,
    int sampleRate,
    float[] output,
    int outputOffset)
  {
    for (var i = 0; i < frameLength; i++)
    {
      state.NoiseIndex++;
      output[outputOffset + i] += 0;
    }
  }

  private static double NoteEnvelope(double position, double smoothness)
  {
    position = Math.Clamp(position, 0, 1);
    var attack = 0.05 + smoothness * 0.15;
    var release = 0.1 + smoothness * 0.2;

    if (position < attack)
      return position / attack;

    if (position > 1.0 - release)
      return Math.Max(0, (1.0 - position) / release);

    return 1.0;
  }

  private static double BurstEnvelope(int sampleInFrame, int burstSamples, bool enabled)
  {
    if (!enabled || burstSamples <= 0)
      return 0;

    if (sampleInFrame >= burstSamples)
      return 0;

    var t = sampleInFrame / (double)burstSamples;
    return (1.0 - t) * (1.0 - t);
  }

  private static double FormantFilter(SynthState state, double source, TimbreProfile profile)
  {
    var f1 = Resonator(state.F1, source, profile.Formant1Hz, profile.Formant1BwHz);
    var f2 = Resonator(state.F2, source, profile.Formant2Hz, profile.Formant2BwHz);
    var f3 = Resonator(state.F3, source, profile.Formant3Hz, profile.Formant3BwHz);
    return 0.55 * f1 + 0.3 * f2 + 0.15 * f3;
  }

  private static double Resonator(ResonatorState resonator, double input, double frequencyHz, double bandwidthHz)
  {
    var r = Math.Exp(-Math.PI * bandwidthHz / Math.Max(frequencyHz, 1.0));
    var theta = TwoPi * frequencyHz / 44100.0; // fixed rate — matches DefaultSampleRate
    var cosine = Math.Cos(theta);

    var output = input + 2.0 * r * cosine * resonator.Y1 - r * r * resonator.Y2;
    resonator.Y2 = resonator.Y1;
    resonator.Y1 = output;
    return output;
  }

  private static double NasalResonance(double input, double nasal, SynthState state)
  {
    if (nasal <= 0)
      return input;

    var anti = input - state.NasalMemory * 0.35;
    state.NasalMemory = input;
    return input * (1.0 - nasal) + anti * nasal;
  }

  private static double HighShelf(double input, double brightness)
  {
    var high = input - statefulHigh(input);
    return input * (1.0 - brightness * 0.35) + high * (0.35 + brightness * 0.65);
  }

  private static double statefulHigh(double input) => input * 0.85;

  private static double DeterministicNoise(long index)
  {
    var x = Math.Sin(index * 12.9898 + 78.233) * 43758.5453;
    return x - Math.Floor(x) * 2.0 - 1.0;
  }

  private static double VelocityToAmplitude(int velocity) =>
    Math.Pow(Math.Clamp(velocity, 1, 127) / 127.0, 1.4) * 0.35;

  private static void Normalize(float[] samples)
  {
    var peak = 0.0f;
    foreach (var sample in samples)
      peak = Math.Max(peak, Math.Abs(sample));

    if (peak <= 1e-6f)
      return;

    var scale = 0.95f / peak;
    for (var i = 0; i < samples.Length; i++)
      samples[i] *= scale;
  }

  private sealed class SynthState
  {
    public double Phase { get; set; }
    public long NoiseIndex { get; set; }
    public double NasalMemory { get; set; }
    public ResonatorState F1 { get; } = new();
    public ResonatorState F2 { get; } = new();
    public ResonatorState F3 { get; } = new();
  }

  private sealed class ResonatorState
  {
    public double Y1 { get; set; }
    public double Y2 { get; set; }
  }
}
