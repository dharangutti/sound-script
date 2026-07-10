using SoundScript.Timbre;
using Xunit;

namespace SoundScript.Tests;

public class SoundCssDspMappingTests
{
    private static readonly CanonicalVoiceMetadata Voice = CanonicalVoiceMetadata.Default;

    [Fact]
    public void Map_Child_MatchesDocumentedMapping()
    {
        var plan = SoundCssDspMapper.Map(new SoundCssPronunciation { Age = SoundCssAge.Child }, Voice);

        // child => +5 semitones, speed x1.15 (duration 1/1.15), formants up 1.15.
        Assert.Equal(5.0, plan.PitchSemitones, 2);
        Assert.Equal(1.0 / 1.15, plan.TimeStretch, 4);
        Assert.Equal(1.15, plan.FormantShift, 3);
    }

    [Theory]
    [InlineData(SoundCssEnergy.High, 4.0)]
    [InlineData(SoundCssEnergy.Medium, 0.0)]
    [InlineData(SoundCssEnergy.Low, -4.0)]
    public void Map_Energy_SetsGainDb(SoundCssEnergy energy, double expectedGain)
    {
        var plan = SoundCssDspMapper.Map(new SoundCssPronunciation { Energy = energy }, Voice);
        Assert.Equal(expectedGain, plan.GainDb, 3);
    }

    [Fact]
    public void Map_Gender_ShiftsPitchAndFormants()
    {
        var male = SoundCssDspMapper.Map(new SoundCssPronunciation { Gender = SoundCssGender.Male }, Voice);
        Assert.Equal(-4.0, male.PitchSemitones, 2);
        Assert.Equal(0.92, male.FormantShift, 3);

        var female = SoundCssDspMapper.Map(new SoundCssPronunciation { Gender = SoundCssGender.Female }, Voice);
        Assert.Equal(4.0, female.PitchSemitones, 2);
        Assert.Equal(1.08, female.FormantShift, 3);
    }

    [Theory]
    [InlineData(SoundCssSpeedMode.Fast, null, 1.0 / 1.15)]
    [InlineData(SoundCssSpeedMode.Slow, null, 1.0 / 0.85)]
    [InlineData(SoundCssSpeedMode.Explicit, 0.8, 1.0 / 0.8)]
    [InlineData(SoundCssSpeedMode.Explicit, 1.2, 1.0 / 1.2)]
    public void Map_Speed_SetsTimeStretch(SoundCssSpeedMode mode, double? multiplier, double expected)
    {
        var plan = SoundCssDspMapper.Map(
            new SoundCssPronunciation { Speed = new SoundCssSpeed(mode, multiplier) }, Voice);
        Assert.Equal(expected, plan.TimeStretch, 4);
    }

    [Fact]
    public void Map_Pitch_IsRelativeToBaseAndBounded()
    {
        var plan = SoundCssDspMapper.Map(new SoundCssPronunciation { PitchSemitones = 3 }, Voice);
        Assert.Equal(3.0, plan.PitchSemitones, 2);
        Assert.Equal(Voice.BasePitchHz * Math.Pow(2, 3.0 / 12.0), plan.TargetPitchHz, 2);
    }

    [Fact]
    public void Map_ExtremePitch_ClampsToHumanBand()
    {
        // +28 semitones from 120 Hz would exceed the 500 Hz ceiling → clamped.
        var plan = SoundCssDspMapper.Map(
            new SoundCssPronunciation { PitchSemitones = 24, Gender = SoundCssGender.Female }, Voice);

        Assert.Equal(500.0, plan.TargetPitchHz, 3);
        Assert.True(plan.PitchSemitones < 25.0);
        Assert.Equal(12.0 * Math.Log2(500.0 / Voice.BasePitchHz), plan.PitchSemitones, 3);
    }

    [Fact]
    public void Map_Vibrato_Accumulates()
    {
        var none = SoundCssDspMapper.Map(new SoundCssPronunciation { Vibrato = SoundCssVibrato.None }, Voice);
        Assert.False(none.Vibrato.IsActive);

        var strong = SoundCssDspMapper.Map(new SoundCssPronunciation { Vibrato = SoundCssVibrato.Strong }, Voice);
        Assert.True(strong.Vibrato.IsActive);
        Assert.Equal(0.7, strong.Vibrato.DepthSemitones, 3);
        Assert.Equal(6.0, strong.Vibrato.RateHz, 3);
    }

    [Fact]
    public void MapPersona_Robot_IsMonotoneWithNoiseAndMetallicEq()
    {
        var plan = SoundCssDspMapper.MapPersona(SoundCssPersona.Robot, Voice);

        Assert.False(plan.Vibrato.IsActive);
        Assert.True(plan.NoiseLayer > 0);
        Assert.Contains(plan.EqBands, b => b is { Shelf: EqShelf.Peak } && Math.Abs(b.PivotHz - 1500) < 1);
    }

    [Fact]
    public void MapPersona_Bright_BoostsHighs()
    {
        var plan = SoundCssDspMapper.MapPersona(SoundCssPersona.Bright, Voice);
        Assert.Contains(plan.EqBands, b => b.Shelf == EqShelf.HighShelf && b.GainDb > 0);
    }

    [Fact]
    public void MapPersona_Soft_IsQuieterAndBreathy()
    {
        var plan = SoundCssDspMapper.MapPersona(SoundCssPersona.Soft, Voice);
        Assert.True(plan.GainDb < 0);
        Assert.True(plan.NoiseLayer > 0);
    }

    [Fact]
    public void Map_PersonaThenAttribute_Composes()
    {
        // Robot base + explicit female pitch: pitch comes from the attribute,
        // robot's noise/EQ remain.
        var plan = SoundCssDspMapper.Map(
            new SoundCssPronunciation { Persona = SoundCssPersona.Robot, Gender = SoundCssGender.Female }, Voice);

        Assert.Equal(4.0, plan.PitchSemitones, 2);
        Assert.True(plan.NoiseLayer > 0);
    }

    [Fact]
    public void Map_IsDeterministic()
    {
        var pron = new SoundCssPronunciation
        {
            Persona = SoundCssPersona.Narrator,
            Style = SoundCssStyle.Sing,
            PitchSemitones = 2,
        };

        var a = SoundCssDspMapper.Map(pron, Voice);
        var b = SoundCssDspMapper.Map(pron, Voice);

        Assert.Equal(a.PitchSemitones, b.PitchSemitones);
        Assert.Equal(a.TimeStretch, b.TimeStretch);
        Assert.Equal(a.GainDb, b.GainDb);
        Assert.Equal(a.FormantShift, b.FormantShift);
        Assert.Equal(a.NoiseLayer, b.NoiseLayer);
        Assert.Equal(a.Vibrato, b.Vibrato);
        Assert.Equal(a.EqBands, b.EqBands);
    }

    [Fact]
    public void Map_FromParsedWordRule_Integrates()
    {
        var pronunciations = SoundCSSParser.ParsePronunciations("\"hi\" { persona: robot; pitch: -2; }");
        var plan = SoundCssDspMapper.Map(pronunciations["hi"], Voice);

        Assert.Equal(-2.0, plan.PitchSemitones, 2);
        Assert.True(plan.NoiseLayer > 0);
    }
}
