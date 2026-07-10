using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

public class WordPronunciationParserTests
{
    [Fact]
    public void ParsePronunciations_ReadsAllAttributes()
    {
        var source = """
            "hello" {
                style: sing;
                accent: uk;
                speed: x1.2;
                pitch: +2;
                energy: high;
                timbre: bright;
                gender: female;
                age: adult;
                persona: narrator;
                emotion: happy;
                breath: low;
                vibrato: medium;
            }
            """;

        var pronunciations = SoundCSSParser.ParsePronunciations(source);
        var p = pronunciations["hello"];

        Assert.Equal("hello", p.Word);
        Assert.Equal(SoundCssStyle.Sing, p.Style);
        Assert.Equal(SoundCssAccent.Uk, p.Accent);
        Assert.Equal(SoundCssSpeedMode.Explicit, p.Speed!.Mode);
        Assert.Equal(1.2, p.Speed.Multiplier);
        Assert.Equal(2.0, p.PitchSemitones);
        Assert.Equal(SoundCssEnergy.High, p.Energy);
        Assert.Equal(SoundCssTimbre.Bright, p.Timbre);
        Assert.Equal(SoundCssGender.Female, p.Gender);
        Assert.Equal(SoundCssAge.Adult, p.Age);
        Assert.Equal(SoundCssPersona.Narrator, p.Persona);
        Assert.Equal(SoundCssEmotion.Happy, p.Emotion);
        Assert.Equal(SoundCssBreath.Low, p.Breath);
        Assert.Equal(SoundCssVibrato.Medium, p.Vibrato);
    }

    [Theory]
    [InlineData("style: normal;", "hello")]
    [InlineData("style: sing;", "hello")]
    [InlineData("style: whisper;", "hello")]
    [InlineData("style: shout;", "hello")]
    [InlineData("accent: usa;", "hello")]
    [InlineData("accent: uk;", "hello")]
    [InlineData("accent: india;", "hello")]
    [InlineData("energy: high;", "hello")]
    [InlineData("energy: medium;", "hello")]
    [InlineData("energy: low;", "hello")]
    [InlineData("timbre: bright;", "hello")]
    [InlineData("timbre: dark;", "hello")]
    [InlineData("timbre: flat;", "hello")]
    [InlineData("gender: male;", "hello")]
    [InlineData("gender: female;", "hello")]
    [InlineData("gender: neutral;", "hello")]
    [InlineData("age: child;", "hello")]
    [InlineData("age: teen;", "hello")]
    [InlineData("age: adult;", "hello")]
    [InlineData("age: senior;", "hello")]
    [InlineData("persona: narrator;", "hello")]
    [InlineData("persona: robot;", "hello")]
    [InlineData("persona: soft;", "hello")]
    [InlineData("persona: bright;", "hello")]
    [InlineData("emotion: happy;", "hello")]
    [InlineData("emotion: sad;", "hello")]
    [InlineData("emotion: angry;", "hello")]
    [InlineData("emotion: calm;", "hello")]
    [InlineData("emotion: excited;", "hello")]
    [InlineData("breath: none;", "hello")]
    [InlineData("breath: low;", "hello")]
    [InlineData("breath: medium;", "hello")]
    [InlineData("breath: high;", "hello")]
    [InlineData("vibrato: none;", "hello")]
    [InlineData("vibrato: light;", "hello")]
    [InlineData("vibrato: medium;", "hello")]
    [InlineData("vibrato: strong;", "hello")]
    [InlineData("speed: fast;", "hello")]
    [InlineData("speed: slow;", "hello")]
    [InlineData("speed: x0.8;", "hello")]
    [InlineData("pitch: +5;", "hello")]
    [InlineData("pitch: -12;", "hello")]
    public void ParsePronunciations_AcceptsValidValues(string declaration, string word)
    {
        var source = $"\"{word}\" {{ {declaration} }}";
        var pronunciations = SoundCSSParser.ParsePronunciations(source);
        Assert.True(pronunciations.ContainsKey(word));
    }

    [Theory]
    [InlineData("style: mumble;")]
    [InlineData("accent: french;")]
    [InlineData("energy: loud;")]
    [InlineData("timbre: warm;")]
    [InlineData("gender: robot;")]
    [InlineData("age: baby;")]
    [InlineData("persona: hero;")]
    [InlineData("emotion: bored;")]
    [InlineData("breath: heavy;")]
    [InlineData("vibrato: wild;")]
    [InlineData("speed: turbo;")]
    [InlineData("speed: x0;")]
    [InlineData("speed: x99;")]
    [InlineData("pitch: up;")]
    [InlineData("pitch: +99;")]
    [InlineData("pitch: -30;")]
    [InlineData("loudness: high;")]
    public void ParsePronunciations_RejectsInvalidValues(string declaration)
    {
        var source = $"\"hello\" {{ {declaration} }}";
        Assert.Throws<FormatException>(() => SoundCSSParser.ParsePronunciations(source));
    }

    [Fact]
    public void ParsePronunciations_SingleLineBlock()
    {
        var pronunciations = SoundCSSParser.ParsePronunciations("\"star\" { style: sing; pitch: -2; }");
        var p = pronunciations["star"];
        Assert.Equal(SoundCssStyle.Sing, p.Style);
        Assert.Equal(-2.0, p.PitchSemitones);
    }

    [Fact]
    public void ParsePronunciations_IsCaseInsensitiveOnWordLookup()
    {
        var pronunciations = SoundCSSParser.ParsePronunciations("\"Hello\" { style: sing; }");
        Assert.True(pronunciations.ContainsKey("hello"));
        Assert.True(pronunciations.ContainsKey("HELLO"));
    }

    [Fact]
    public void ParsePronunciations_LaterDuplicateWins()
    {
        var source = """
            "hi" { style: sing; }
            "hi" { style: whisper; }
            """;
        Assert.Equal(SoundCssStyle.Whisper, SoundCSSParser.ParsePronunciations(source)["hi"].Style);
    }

    [Fact]
    public void ToTransformPlan_IsDeterministicAndOrdered()
    {
        var source = """
            "song" {
                vibrato: strong;
                style: sing;
                pitch: +3;
                accent: usa;
                speed: x1.5;
            }
            """;

        var planA = SoundCSSParser.ParseTransformPlans(source)["song"];
        var planB = SoundCSSParser.ParseTransformPlans(source)["song"];

        // Deterministic: same word and identical directive sequence across runs.
        Assert.Equal(planA.Word, planB.Word);
        Assert.Equal(planA.Directives, planB.Directives);

        // Directives emit in canonical TransformKind order regardless of source order.
        Assert.Equal(
            [TransformKind.Style, TransformKind.Accent, TransformKind.Speed, TransformKind.Pitch, TransformKind.Vibrato],
            planA.Directives.Select(d => d.Kind).ToArray());

        var speed = planA.Directives.Single(d => d.Kind == TransformKind.Speed);
        Assert.Equal("x1.5", speed.Value);
        Assert.Equal(1.5, speed.Numeric);

        var pitch = planA.Directives.Single(d => d.Kind == TransformKind.Pitch);
        Assert.Equal("+3", pitch.Value);
        Assert.Equal(3.0, pitch.Numeric);

        var style = planA.Directives.Single(d => d.Kind == TransformKind.Style);
        Assert.Equal("sing", style.Value);
    }

    [Fact]
    public void ToTransformPlan_SpeedKeywordsHaveNoMultiplier()
    {
        var plan = SoundCSSParser.ParseTransformPlans("\"go\" { speed: fast; }")["go"];
        var speed = plan.Directives.Single();
        Assert.Equal("fast", speed.Value);
        Assert.Null(speed.Numeric);
    }

    [Fact]
    public void ExistingPhonemeStylesheet_StillParsesUnchanged()
    {
        var source = """
            @phonemes t w aa
            p {
                burst: 12ms;
                noise: 0.3;
            }
            aa { formant1: 700Hz; }
            """;

        var profiles = SoundCSSParser.ParseOverrides(source);
        Assert.Equal(12, PhonemeTimbreMapper.Map("p", profiles).BurstMs);
        Assert.Equal(700, PhonemeTimbreMapper.Map("aa", profiles).Formant1Hz);

        // No word rules present.
        Assert.Empty(SoundCSSParser.ParsePronunciations(source));
    }

    [Fact]
    public void MixedStylesheet_SeparatesPhonemeAndWordRules()
    {
        var source = """
            p { burst: 10ms; }
            "twinkle" { style: sing; pitch: +2; }
            aa { formant1: 650Hz; }
            "star" { style: whisper; }
            """;

        var profiles = SoundCSSParser.ParseOverrides(source);
        Assert.Equal(10, PhonemeTimbreMapper.Map("p", profiles).BurstMs);
        Assert.Equal(650, PhonemeTimbreMapper.Map("aa", profiles).Formant1Hz);
        Assert.False(profiles.ContainsKey("\"twinkle\""));

        var pronunciations = SoundCSSParser.ParsePronunciations(source);
        Assert.Equal(2, pronunciations.Count);
        Assert.Equal(SoundCssStyle.Sing, pronunciations["twinkle"].Style);
        Assert.Equal(SoundCssStyle.Whisper, pronunciations["star"].Style);
    }

    [Fact]
    public void ParsePronunciations_UnknownAttribute_Throws()
    {
        var ex = Assert.Throws<FormatException>(
            () => SoundCSSParser.ParsePronunciations("\"hello\" { wobble: 3; }"));
        Assert.Contains("wobble", ex.Message);
    }
}
