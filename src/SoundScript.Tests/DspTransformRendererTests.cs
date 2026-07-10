using System.Security.Cryptography;
using SoundScript.Timbre;
using SoundScript.Vocal;
using SoundScript.Wave.Io;
using SoundScript.Wave.Synthesis;
using Xunit;

namespace SoundScript.Tests;

public class DspTransformRendererTests
{
    private const int SampleRate = WavWriter.SampleRate;
    private const int Seed = 1234;

    private static float[] TestTone()
    {
        // Deterministic 0.2 s tone at 220 Hz (DeterministicMath → cross-platform stable).
        var samples = new float[SampleRate / 5];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(0.5 * DeterministicMath.Sin(2.0 * Math.PI * 220.0 * i / SampleRate));

        return samples;
    }

    [Theory]
    [InlineData(SoundCssPersona.Narrator)]
    [InlineData(SoundCssPersona.Robot)]
    [InlineData(SoundCssPersona.Soft)]
    [InlineData(SoundCssPersona.Bright)]
    public void Render_Persona_IsReproducibleAcrossRuns(SoundCssPersona persona)
    {
        var input = TestTone();
        var plan = SoundCssDspMapper.MapPersona(persona, CanonicalVoiceMetadata.Default);

        var hashA = RenderHash(input, plan);
        var hashB = RenderHash(input, plan);

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void Render_Personas_ProduceDistinctAudio()
    {
        var input = TestTone();
        var personas = new[]
        {
            SoundCssPersona.Narrator, SoundCssPersona.Robot, SoundCssPersona.Soft, SoundCssPersona.Bright,
        };

        var hashes = personas
            .Select(p => RenderHash(input, SoundCssDspMapper.MapPersona(p, CanonicalVoiceMetadata.Default)))
            .ToArray();

        Assert.Equal(hashes.Length, hashes.Distinct().Count());
    }

    [Fact]
    public void Render_IdentityPlan_ReturnsInputUnchanged()
    {
        var input = TestTone();
        var plan = SoundCssDspMapper.Map(new SoundCssPronunciation(), CanonicalVoiceMetadata.Default);

        var output = DspTransformRenderer.Render(input, plan, Seed);

        Assert.Equal(input, output);
    }

    [Fact]
    public void Render_ParsedWordRule_IsReproducible()
    {
        var input = TestTone();
        var pron = SoundCSSParser.ParsePronunciations("\"sing\" { style: sing; pitch: +3; vibrato: medium; }")["sing"];
        var plan = SoundCssDspMapper.Map(pron, CanonicalVoiceMetadata.Default);

        Assert.Equal(RenderHash(input, plan), RenderHash(input, plan));
    }

    [Fact]
    public void Render_Gain_ScalesAmplitude()
    {
        var input = TestTone();
        var plan = new DspTransformPlan { GainDb = 6.0 };

        var output = DspTransformRenderer.Render(input, plan, Seed);

        Assert.Equal(input.Length, output.Length);
        Assert.True(Peak(output) > Peak(input));
    }

    private static string RenderHash(float[] input, DspTransformPlan plan)
    {
        var output = DspTransformRenderer.Render(input, plan, Seed);
        using var stream = new MemoryStream();
        WavWriter.WriteTo(stream, output, SampleRate);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static double Peak(float[] samples)
    {
        var peak = 0.0;
        foreach (var s in samples)
            peak = Math.Max(peak, Math.Abs(s));
        return peak;
    }
}
