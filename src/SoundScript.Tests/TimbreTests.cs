using SoundScript.Compose;
using SoundScript.Midi;
using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

public class SoundCSSParserTests
{
    private const string Sample = """
        // plosive styling
        p {
            burst: 12ms;
            noise: 0.3;
            brightness: 0.2;
        }

        aa {
            formant1: 700Hz;
            formant2: 1100Hz;
            smoothness: 0.9;
        }
        """;

    [Fact]
    public void Parse_ReadsCycleLevelAttributes()
    {
        var source = """
            aa {
                harmonic1: 0.9;
                harmonic2: 0.6;
                noise-fricative: 0.2;
                transient: 8ms;
            }
            """;
        var profiles = SoundCSSParser.ParseOverrides(source);
        var mapped = PhonemeTimbreMapper.Map("aa", profiles);

        Assert.Equal(0.9, mapped.Harmonic1, 3);
        Assert.Equal(0.6, mapped.Harmonic2, 3);
        Assert.Equal(0.2, mapped.NoiseFricative, 3);
        Assert.Equal(8, mapped.TransientMs);
    }

    [Fact]
    public void Parse_ReadsPhonemeProfiles()
    {
        var profiles = SoundCSSParser.ParseOverrides(Sample);

        Assert.Equal(12, PhonemeTimbreMapper.Map("p", profiles).BurstMs);
        Assert.Equal(0.3, PhonemeTimbreMapper.Map("p", profiles).Noise, 3);
        Assert.Equal(0.2, PhonemeTimbreMapper.Map("p", profiles).Brightness, 3);
        Assert.Equal(700, PhonemeTimbreMapper.Map("aa", profiles).Formant1Hz);
        Assert.Equal(1100, PhonemeTimbreMapper.Map("aa", profiles).Formant2Hz);
        Assert.Equal(0.9, PhonemeTimbreMapper.Map("aa", profiles).Smoothness, 3);
    }

    [Fact]
    public void ParsePhonemeSequence_ReadsDirective()
    {
        var source = """
            @phonemes t w ai n;
            p { burst: 10ms; }
            """;

        Assert.Equal(["t", "w", "ai", "n"], SoundCSSParser.ParsePhonemeSequence(source));
    }

    [Fact]
    public void Parse_UnknownPropertyThrows()
    {
        Assert.Throws<FormatException>(() => SoundCSSParser.Parse("p { unknown: 1; }"));
    }
}

public class PhonemeTimbreMapperTests
{
    [Theory]
    [InlineData("p", 14, 0.35)]
    [InlineData("aa", 700, 0.05)]
    [InlineData("s", 4, 0.82)]
    public void Map_BuiltInTableMatchesCategory(string phoneme, double burstOrF1, double noise)
    {
        var profile = PhonemeTimbreMapper.Map(phoneme);
        if (phoneme is "aa")
        {
            Assert.Equal(burstOrF1, profile.Formant1Hz);
            Assert.Equal(noise, profile.Noise, 2);
        }
        else if (phoneme is "p" or "s")
        {
            Assert.Equal(burstOrF1, profile.BurstMs);
            Assert.Equal(noise, profile.Noise, 2);
        }
    }

    [Fact]
    public void Map_UnknownPhonemeUsesDefault()
    {
        Assert.Equal(PhonemeTimbreMapper.DefaultProfile, PhonemeTimbreMapper.Map("zz"));
    }

    [Fact]
    public void PhonemesFromText_MatchesComposePipeline()
    {
        var phonemes = PhonemeTimbreMapper.PhonemesFromText("star");
        Assert.Equal("s|t|aa|r", string.Join("|", phonemes));
    }

    [Fact]
    public void Map_AppliesCssOverrides()
    {
        var css = SoundCSSParser.ParseOverrides("aa { formant1: 800Hz; noise: 0.1; }");
        Assert.Equal(800, PhonemeTimbreMapper.Map("aa", css).Formant1Hz);
        Assert.Equal(0.1, PhonemeTimbreMapper.Map("aa", css).Noise, 3);
    }
}

public class MidiToTimbreTimelineTests
{
    private static byte[] ComposeMidi(string text)
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram(text), stream);
        return stream.ToArray();
    }

    [Fact]
    public void Build_ProducesSegmentsForEachPhonemeNote()
    {
        var midiBytes = ComposeMidi("star");
        var temp = Path.Combine(Path.GetTempPath(), $"ss-timeline-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, midiBytes);

        try
        {
            var phonemes = PhonemeTimbreMapper.PhonemesFromText("star");
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: phonemes,
                preferredTrackName: null);

            Assert.Equal(phonemes.Count, timeline.Segments.Count);
            Assert.Equal("s", timeline.Segments[0].Phoneme);
            Assert.True(timeline.TotalDurationMs > 0);
            Assert.Equal(8.0, timeline.FrameMs);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Build_FrameCountCoversDuration()
    {
        var midiBytes = ComposeMidi("la");
        var temp = Path.Combine(Path.GetTempPath(), $"ss-timeline-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, midiBytes);

        try
        {
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: PhonemeTimbreMapper.PhonemesFromText("la"),
                preferredTrackName: null);

            Assert.True(timeline.FrameCount >= 1);
            Assert.NotEmpty(timeline.Frames);
            Assert.True(timeline.SampleCount >= timeline.FrameCount);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}

public class SpectralEngineTests
{
    [Fact]
    public void Synthesize_ProducesNonSilentAudio()
    {
        var midiBytes = RenderTestMidi("mi");
        var temp = Path.Combine(Path.GetTempPath(), $"ss-spec-{Guid.NewGuid():N}.mid");
        File.WriteAllBytes(temp, midiBytes);

        try
        {
            var timeline = MidiToTimbreTimeline.Build(
                temp,
                phonemes: PhonemeTimbreMapper.PhonemesFromText("mi"),
                preferredTrackName: null);
            var samples = SpectralEngine.Synthesize(timeline);

            Assert.Equal(timeline.SampleCount, samples.Length);
            Assert.Contains(samples, s => Math.Abs(s) > 0.001f);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Synthesize_IsDeterministic()
    {
        var timeline = BuildShortTimeline();
        var a = SpectralEngine.Synthesize(timeline);
        var b = SpectralEngine.Synthesize(timeline);
        Assert.Equal(a, b);
    }

    private static TimbreTimeline BuildShortTimeline()
    {
        var segments = new List<TimbreSegment>
        {
            new(0, 180, 440, 64, "aa", PhonemeTimbreMapper.Map("aa"))
        };
        return new TimbreTimeline
        {
            SampleRate = 44100,
            FrameMs = 8,
            TotalDurationMs = 200,
            Segments = segments,
            Frames = MidiToTimbreTimeline.BuildFramePlans(segments, 8, 200)
        };
    }

    private static byte[] RenderTestMidi(string text)
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram(text), stream);
        return stream.ToArray();
    }
}

public class OfflineRendererTests
{
    private const string Example = "Twinkle twinkle little star";

    [Fact]
    public void RenderToWavBytes_IsDeterministicSha256()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram(Example), stream);
        var midiBytes = stream.ToArray();

        var options = new OfflineRenderer.RenderOptions { SourceText = Example };
        var hashA = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);
        var hashB = OfflineRenderer.RenderSha256(midiBytes, OfflineRenderer.DefaultStylesheet, options);

        Assert.Equal(hashA, hashB);
        Assert.Matches("^[0-9A-F]{64}$", hashA);
    }

    [Fact]
    public void RenderToWavBytes_ProducesValidWavHeader()
    {
        using var stream = new MemoryStream();
        MidiGenerator.Write(PhonemeComposer.ComposeProgram("la"), stream);
        var wav = OfflineRenderer.RenderToWavBytes(
            stream.ToArray(),
            options: new OfflineRenderer.RenderOptions { SourceText = "la" });

        Assert.Equal("RIFF"u8.ToArray(), wav.AsSpan(0, 4).ToArray());
        Assert.Equal("WAVE"u8.ToArray(), wav.AsSpan(8, 4).ToArray());
    }

    [Fact]
    public void AudioWriter_EncodeOgg_IsDeterministic()
    {
        var samples = new float[1024];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(i * 0.05);

        var a = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(AudioWriter.EncodeOgg(samples, 44100)));
        var b = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(AudioWriter.EncodeOgg(samples, 44100)));
        Assert.Equal(a, b);
    }
}
