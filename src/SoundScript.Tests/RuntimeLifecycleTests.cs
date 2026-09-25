using System.Security.Cryptography;
using SoundScript.Media;
using Xunit;

namespace SoundScript.Tests;

public class RuntimeLifecycleTests
{
    [Fact]
    public void InstancesReuseCompilationButStartWithIndependentDefaults()
    {
        var first = SoundScriptEngine.CompileRuntime(RuntimeBindingTests.Source);
        var defaults = first.Bind();
        first.Set("intensity", .9m);
        var second = first.CreateInstance();
        Assert.Same(first.Parameters, second.Parameters); // immutable schema is intentionally shared
        Assert.Equal(.25m, second.Get("intensity"));
        Assert.Equal(0, second.Revision);
        second.Set("xpos", 900m);
        Assert.Equal(200m, first.Get("xpos"));
        Assert.Equal(defaults.RenderAudio(), second.RenderAudio());
        Assert.NotEqual(TemporalVisualJson.Serialize(defaults.SceneAt(TimeSpan.FromSeconds(2))),
            TemporalVisualJson.Serialize(second.SceneAt(TimeSpan.FromSeconds(2))));
        first.Reset();
        Assert.Equal(900m, second.Get("xpos"));
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), first.Statistics);
        Assert.Equal(first.Statistics, second.Statistics);
        Assert.Equal(defaults.RenderMidi(), second.Bind().RenderMidi());
        // No IDisposable/lifecycle handle exists: releasing a host reference does
        // not invalidate a retained snapshot or another runtime instance.
        var retained = second.Bind();
        second = null;
        Assert.Equal(defaults.RenderAudio(), retained.RenderAudio());
    }

    [Fact]
    public async Task ConcurrentBatchesAndReadersObserveOnlyCompleteStates()
    {
        var runtime = SoundScriptEngine.CompileRuntime(RuntimeBindingTests.Source);
        var states = new[]
        {
            new Dictionary<string, decimal> { ["intensity"] = .25m, ["xpos"] = 200m },
            new Dictionary<string, decimal> { ["intensity"] = .9m, ["xpos"] = 900m }
        };
        var expected = new Dictionary<decimal, (decimal X, string Audio)>();
        foreach (var values in states)
        {
            runtime.SetMany(values);
            expected[values["intensity"]] = (values["xpos"], Hash(runtime.RenderAudio()));
        }
        var writer = Task.Run(async () => { for (var i = 0; i < 5000; i++) { runtime.SetMany(states[i % 2]); await Task.Yield(); } });
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 12; i++)
            {
                var snapshot = runtime.Bind();
                var p = Assert.Single(snapshot.SceneAt(TimeSpan.FromSeconds(2)).Primitives);
                Assert.Equal(expected[p.Opacity].X, p.Left + p.Width / 2);
                Assert.Equal(expected[p.Opacity].Audio, Hash(snapshot.RenderAudio()));
            }
        }));
        await Task.WhenAll(readers.Append(writer));
        Assert.Equal(new RuntimeCompilationStatistics(1, 1, 1), runtime.Statistics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Intensity")]
    [InlineData("tempo")]
    public void InvalidNamesExplainDiscoveryAndCase(string? name)
    {
        var runtime = SoundScriptEngine.CompileRuntime(RuntimeBindingTests.Source);
        var error = Assert.Throws<ArgumentException>(() => runtime.Set(name!, .5m));
        Assert.Contains("case-sensitive", error.Message);
        Assert.True(error.Message.Contains("Parameters") || error.Message.Contains("intensity"));
        Assert.Equal(0, runtime.Revision);
    }

    [Fact]
    public void WrongCategoriesAndPreciseBoundariesExplainParameterAndConstraint()
    {
        var runtime = SoundScriptEngine.CompileRuntime(RuntimeBindingTests.Source);
        foreach (var value in new object?[] { null, "", "0.5", 1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var error = Assert.Throws<ArgumentException>(() => runtime.Set("intensity", value));
            Assert.Contains("intensity", error.Message); Assert.Contains("System.Decimal", error.Message);
        }
        foreach (var value in new[] { 0m, 1m }) runtime.Set("intensity", value);
        foreach (var value in new[] { -.0000000000000000000000000001m, 1.0000000000000000000000000001m })
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => runtime.SetMany(new Dictionary<string, decimal>
                { ["xpos"] = 800, ["intensity"] = value }));
            Assert.Contains("intensity", error.Message); Assert.Contains("0 through 1", error.Message);
            Assert.Equal(200m, runtime.Get("xpos"));
        }
        runtime.Set("intensity", .5m); // recovery after rejection
        Assert.Contains("intensity=0.5", runtime.ToString());
        Assert.DoesNotContain("ProgramNode", runtime.ToString());
    }

    [Fact]
    public void ReturnedMediaAndMetadataCannotCorruptTemplateOrSnapshot()
    {
        var runtime = SoundScriptEngine.CompileRuntime(RuntimeBindingTests.Source);
        var snapshot = runtime.Bind();
        var expected = Hash(snapshot.RenderAudio());
        var bytes = snapshot.RenderAudio(); Array.Fill(bytes, (byte)0);
        Assert.Equal(expected, Hash(snapshot.RenderAudio()));
        var collection = Assert.IsAssignableFrom<IList<SoundScriptRuntimeParameter>>(runtime.Parameters);
        Assert.Throws<NotSupportedException>(() => collection[0] = new("injected", 5, 0, 10));
        var changedRecord = runtime.Parameters[0] with { Default = 999 };
        Assert.NotEqual(changedRecord.Default, runtime.Parameters[0].Default);
        Assert.Equal(.25m, runtime.Get("intensity"));
    }

    [Fact]
    public void StaticRuntimeAndRequiredDefaultBehaviourAreExplicit()
    {
        var runtime = SoundScriptEngine.CompileRuntime("track cue { C4 q }");
        Assert.Empty(runtime.Parameters); Assert.Contains("no parameters", runtime.ToString());
        Assert.Contains("no runtime parameters", Assert.Throws<ArgumentException>(() => runtime.Set("gain", .5m)).Message);
        Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.CompileRuntime("param xpos visual \"v\" for 1s { set x xpos }"));
        Assert.Throws<InvalidOperationException>(() => SoundScriptEngine.CompileRuntime("param bad_name = 5 visual \"v\" for 1s { set x bad_name }"));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
