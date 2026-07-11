using SoundScript.Timbre;
using SoundScript.Vocal.Wordbank;
using SoundScript.Wave.Io;

namespace SoundScript.Vocal;

/// <summary>
/// Post-processing stage that turns independently-rendered word stems into one
/// continuous buffer, eliminating the audible word boundaries left by hard,
/// silence-gapped concatenation.
///
/// <para>It threads DSP state across words — carried vibrato LFO phase, a
/// continuous deterministic noise index, and pitch/formant glide toward the
/// previous word — then overlap-adds adjacent stems with an equal-power
/// crossfade (built from <see cref="Math.Sqrt(double)"/>, so it is bit-identical
/// across platforms, matching the repo's pan-law determinism policy).</para>
/// </summary>
public static class ContinuousVocalRenderer
{
    private const int SampleRate = WavWriter.SampleRate;

    /// <summary>
    /// Renders each raw word stem with cross-word DSP continuity, then crossfade-
    /// stitches them into a single buffer. <paramref name="rawStems"/> must align
    /// 1:1 with <paramref name="words"/>.
    /// </summary>
    public static float[] Assemble(
        IReadOnlyList<string> words,
        IReadOnlyList<float[]> rawStems,
        VocalEngineOptions options)
    {
        var rendered = RenderWords(words, rawStems, options);
        return Stitch(rendered, options);
    }

    /// <summary>
    /// Sequentially renders word stems, carrying vibrato phase, a continuous noise
    /// index, and applying pitch/formant glide toward the previous word's plan.
    /// </summary>
    public static IReadOnlyList<RenderedWord> RenderWords(
        IReadOnlyList<string> words,
        IReadOnlyList<float[]> rawStems,
        VocalEngineOptions options)
    {
        if (words.Count != rawStems.Count)
            throw new ArgumentException("words and rawStems must have equal length.");

        var result = new List<RenderedWord>(words.Count);
        var vibratoPhase = 0.0;
        var noiseOffset = 0;
        DspTransformPlan? previous = null;

        for (var i = 0; i < words.Count; i++)
        {
            var stem = rawStems[i];
            var plan = BuildPlan(words[i], stem, options);

            if (previous is not null)
                plan = Glide(previous, plan, options);

            var pcm = stem.Length == 0
                ? stem
                : DspTransformRenderer.Render(stem, plan, options.Seed, SampleRate, vibratoPhase, noiseOffset);

            vibratoPhase = DspTransformRenderer.AdvanceVibratoPhase(vibratoPhase, plan.Vibrato, stem.Length, SampleRate);
            noiseOffset += pcm.Length;
            previous = plan;

            result.Add(new RenderedWord(pcm, plan, words[i]));
        }

        return result;
    }

    /// <summary>Equal-power crossfade-stitches rendered words into one buffer.</summary>
    public static float[] Stitch(IReadOnlyList<RenderedWord> words, VocalEngineOptions options)
    {
        if (words.Count == 0)
            return [];

        var overlap = Math.Max(0, (int)Math.Round(options.CrossfadeMs / 1000.0 * SampleRate));

        var output = new List<float>(words[0].Pcm);
        for (var n = 1; n < words.Count; n++)
        {
            var next = words[n].Pcm;
            if (next.Length == 0)
                continue;

            if (output.Count == 0)
            {
                output.AddRange(next);
                continue;
            }

            var ov = Math.Min(overlap, Math.Min(output.Count, next.Length));
            for (var k = 0; k < ov; k++)
            {
                // Equal-power crossfade: gOut² + gIn² = 1 (no perceived dip).
                var t = (k + 0.5) / ov;
                var gOut = Math.Sqrt(1.0 - t);
                var gIn = Math.Sqrt(t);
                var idx = output.Count - ov + k;
                output[idx] = (float)Math.Clamp(output[idx] * gOut + next[k] * gIn, -1.0, 1.0);
            }

            for (var k = ov; k < next.Length; k++)
                output.Add(next[k]);
        }

        return [.. output];
    }

    private static DspTransformPlan BuildPlan(string word, float[] stem, VocalEngineOptions options)
    {
        if (options.Pronunciations is null
            || stem.Length == 0
            || !options.Pronunciations.TryGetValue(word, out var pronunciation))
        {
            return new DspTransformPlan();
        }

        var basePitch = AudioNormalizeOps.DetectBasePitchHz(stem, SampleRate);
        var metadata = basePitch > 0
            ? CanonicalVoiceMetadata.Default with { BasePitchHz = basePitch }
            : CanonicalVoiceMetadata.Default;

        return SoundCssDspMapper.Map(pronunciation, metadata);
    }

    /// <summary>Pulls the current plan's pitch and formant toward the previous word's.</summary>
    private static DspTransformPlan Glide(DspTransformPlan previous, DspTransformPlan current, VocalEngineOptions options)
    {
        var pitch = Lerp(current.PitchSemitones, previous.PitchSemitones, options.PitchSmoothing);
        var formant = Lerp(current.FormantShift, previous.FormantShift, options.FormantSmoothing);

        return current with { PitchSemitones = pitch, FormantShift = formant };
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * Math.Clamp(t, 0.0, 1.0);
}
