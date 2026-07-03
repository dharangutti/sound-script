using SoundScript.Core;
using SoundScript.Core.Notation;
using SoundScript.Midi;
using Xunit;
using SoundScriptParser = SoundScript.Parser.Parser;

namespace SoundScript.Tests;

public class PlaybackQualityTests
{
    [Fact]
    public void DynamicShaping_SoftensPianoAndHardensForte()
    {
        var piano = DynamicShaper.Apply(DynamicLevel.Piano, 80);
        var forte = DynamicShaper.Apply(DynamicLevel.Forte, 80);

        Assert.True(piano.Velocity < 80);
        Assert.True(forte.Velocity > 80);
    }

    [Fact]
    public void ArticulationShaping_AdjustsStaccatoDurationAndVelocity()
    {
        var shaped = ArticulationShaper.Apply(ArticulationType.Staccato, 80, 1.0);

        Assert.True(shaped.Shaped);
        Assert.Equal(0.47, shaped.DurationBeats);
        Assert.True(shaped.Velocity < 80);
    }

    [Fact]
    public void InstrumentGainRefinement_BoostsVerySoftVelocities()
    {
        var (velocity, refined) = InstrumentGainRefiner.Apply("flute", 30);

        Assert.True(refined);
        Assert.True(velocity > 30);
    }

    [Fact]
    public void DurationNormalizer_RoundsToStableBeatGrid()
    {
        var (duration, normalized) = DurationNormalizer.Apply(0.5000000004);

        Assert.True(normalized);
        Assert.Equal(0.5, duration, 9);
    }

    [Fact]
    public void ExpressiveCurve_AppliesBalancedCurveByDefault()
    {
        var (velocity, applied) = ExpressiveCurve.Apply(64, null);

        Assert.True(applied);
        Assert.NotEqual(64, velocity);
    }

    [Fact]
    public void ChordBalancer_AdjustsVoiceVelocities()
    {
        int[] notes = [48, 55, 60];
        var (velocities, balanced) = ChordBalancer.Apply(notes, 70);

        Assert.True(balanced);
        Assert.Equal(78, velocities[0]);
        Assert.Equal(65, velocities[1]);
        Assert.Equal(74, velocities[2]);
    }

    [Fact]
    public void Interpreter_AppliesPlaybackWarningsForShapedNote()
    {
        const string source = """
            melody {
                f
                staccato C4 q
            }
            """;

        var interpreted = Interpret(source);
        var note = interpreted.Tracks.Single().Notes.Single();

        Assert.Equal(0.47, note.DurationBeats);
        Assert.Contains(interpreted.Warnings, w => w.Contains("Dynamic shaping applied"));
        Assert.Contains(interpreted.Warnings, w => w.Contains("Articulation shaping applied"));
    }

    [Fact]
    public void Interpreter_AppliesChordBalanceWarning()
    {
        const string source = """
            melody {
                Cmaj q
            }
            """;

        var interpreted = Interpret(source);
        Assert.Equal(3, interpreted.Tracks.Single().Notes.Count);
        Assert.Contains(interpreted.Warnings, w => w.Contains("Chord balance applied"));
    }

    [Fact]
    public void ExistingExamples_RemainValid()
    {
        var examplePaths = Directory.GetFiles(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../examples")),
            "*.ss");

        foreach (var path in examplePaths)
        {
            var interpreted = Interpret(File.ReadAllText(path));
            Assert.True(interpreted.Tracks.Count > 0, $"Expected notes in {Path.GetFileName(path)}");
        }
    }

    private static InterpretedProgram Interpret(string source)
    {
        var program = new SoundScriptParser(new SoundScript.Parser.Tokenizer(source).Tokenize()).Parse();
        return Interpreter.Interpret(program);
    }
}
