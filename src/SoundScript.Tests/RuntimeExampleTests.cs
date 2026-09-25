using System.Text.RegularExpressions;
using SoundScript.Media;
using SoundScript.Playground;
using Xunit;

namespace SoundScript.Tests;

public class RuntimeExampleTests
{
    [Fact]
    public void CommittedRuntimeExampleAndWebSourceMatchPlaygroundCanonicalOutputs()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var web = File.ReadAllText(Path.Combine(root, "samples/ProgrammableMediaWeb/Program.cs"));
        var webSource = Regex.Match(web, "CompileRuntime\\(\"\"\"(.*?)\"\"\"\\)", RegexOptions.Singleline).Groups[1].Value;
        Assert.NotEmpty(webSource);
        var sources = Directory.GetFiles(Path.Combine(root, "samples/RuntimeParameters"), "*.ss")
            .Select(File.ReadAllText).Append(webSource).ToArray();
        foreach (var source in sources)
        {
            var runtime = SoundScriptEngine.CompileRuntime(source);
            var expected = SoundScriptEngine.CompileRuntime(RuntimeMediaSession.MonitoringSource);
            foreach (var (gain, xpos) in new[] { (.25m, 200m), (.55m, 550m), (.9m, 900m) })
            {
                var values = new Dictionary<string, decimal> { ["intensity"] = gain, ["xpos"] = xpos };
                runtime.SetMany(values); expected.SetMany(values);
                Assert.Equal(expected.RenderAudio(), runtime.RenderAudio());
                Assert.Equal(TemporalVisualJson.Serialize(expected.SceneAt(TimeSpan.FromSeconds(2))),
                    TemporalVisualJson.Serialize(runtime.SceneAt(TimeSpan.FromSeconds(2))));
                Assert.Equal(expected.Bind().RenderMidi(), runtime.Bind().RenderMidi());
            }
        }
    }
}
