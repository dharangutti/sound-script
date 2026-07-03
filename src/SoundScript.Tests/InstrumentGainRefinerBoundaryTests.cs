using SoundScript.Midi;
using Xunit;

namespace SoundScript.Tests;

public class InstrumentGainRefinerBoundaryTests
{
    [Theory]
    [InlineData(39, 42)]
    [InlineData(30, 32)]
    public void InstrumentGainRefiner_BoostsVelocitiesBelow40(int input, int expected)
    {
        var (velocity, refined) = InstrumentGainRefiner.Apply("flute", input);
        Assert.True(refined);
        Assert.Equal(expected, velocity);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(110)]
    public void InstrumentGainRefiner_DoesNotBoostAtOrAbove40(int input)
    {
        var (velocity, refined) = InstrumentGainRefiner.Apply("flute", input);
        if (input <= 110)
        {
            Assert.Equal(input, velocity);
            Assert.False(refined);
        }
    }

    [Theory]
    [InlineData(111, 105)]
    [InlineData(120, 114)]
    public void InstrumentGainRefiner_ReducesVelocitiesAbove110(int input, int expected)
    {
        var (velocity, refined) = InstrumentGainRefiner.Apply("flute", input);
        Assert.True(refined);
        Assert.Equal(expected, velocity);
    }

    [Fact]
    public void InstrumentGainRefiner_CapsVelocityAt127()
    {
        var (velocity, refined) = InstrumentGainRefiner.Apply("flute", 127);
        Assert.True(refined);
        Assert.Equal(121, velocity);
        Assert.True(velocity <= 127);
    }

    [Fact]
    public void InstrumentGainRefiner_Boundary40And111_AreExclusive()
    {
        var at40 = InstrumentGainRefiner.Apply("flute", 40);
        var at111 = InstrumentGainRefiner.Apply("flute", 111);

        Assert.False(at40.Refined);
        Assert.Equal(40, at40.Velocity);
        Assert.True(at111.Refined);
        Assert.Equal(105, at111.Velocity);
    }
}
