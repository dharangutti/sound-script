using System.Globalization;
using SoundScript.Media;

namespace SoundScript.Playground;

/// <summary>The adaptive panel's production API session, separate from static editor workflows.</summary>
public sealed class RuntimeMediaSession
{
    public const string MonitoringSource = """
        param intensity = 0.25
        param xpos = 200
        perform expressive
        tempo 120
        track cue { gain intensity C4 q E4 q G4 h }
        visual "indicator" for 4s {
            shape circle
            fill "#ef4444"
            set x xpos
            set y 300
            set width 100
            set height 100
            set opacity intensity
        }
        """;
    public SoundScriptRuntimeProgram? Runtime { get; private set; }
    public byte[] Audio { get; private set; } = [];
    public string Svg { get; private set; } = "";
    public string Json { get; private set; } = "";
    public void Clear() { Runtime = null; Audio = []; Svg = ""; Json = ""; }
    public void Compile(string source)
    {
        Clear();
        var candidate = SoundScriptEngine.CompileRuntime(source);
        Render(candidate); Runtime = candidate;
    }
    public void Apply(IReadOnlyDictionary<string, string> inputs)
    {
        var runtime = Runtime ?? throw new InvalidOperationException("Compile the source first.");
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (name, text) in inputs)
        {
            if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException($"{name} requires a decimal number, using a dot for fractions.");
            values.Add(name, value);
        }
        runtime.SetMany(values); Render(runtime);
    }
    public void Reset()
    {
        var runtime = Runtime ?? throw new InvalidOperationException("Compile the source first.");
        runtime.Reset(); Render(runtime);
    }
    private void Render(SoundScriptRuntimeProgram runtime)
    {
        var snapshot = runtime.Bind();
        var scene = snapshot.SceneAt(snapshot.VisualDuration > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : TimeSpan.Zero);
        var audio = snapshot.RenderAudio();
        var svg = TemporalSvgRenderer.Render(scene);
        var json = TemporalVisualJson.Serialize(scene);
        Audio = audio; Svg = svg; Json = json;
    }
}
