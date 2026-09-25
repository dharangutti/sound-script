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
            animate radius 35 -> 110 over 3s
            set opacity intensity
        }
        """;
    public const string GeometrySource = """
        param volume = 0.4
        param xpos = 640
        param ypos = 360
        param width = 240
        param height = 120
        param angle = 0
        param opacity = 0.8
        perform expressive
        tempo 120
        track cue { gain volume C4 q E4 q G4 h }
        visual "tile" for 4s {
            shape rectangle
            fill "#38bdf8"
            set x xpos
            set y ypos
            set width width
            set height height
            set rotation angle
            set opacity opacity
        }
        """;
    public SoundScriptRuntimeProgram? Runtime { get; private set; }
    public byte[] Audio { get; private set; } = [];
    public string Svg { get; private set; } = "";
    public string Json { get; private set; } = "";
    internal SoundScriptRuntimeSnapshot? Snapshot { get; private set; }
    internal TimeSpan VisualDuration => Snapshot?.VisualDuration ?? TimeSpan.Zero;
    internal string SvgAt(double seconds) => TemporalSvgRenderer.Render(
        (Snapshot ?? throw new InvalidOperationException("Compile the source first."))
        .SceneAt(TimeSpan.FromSeconds(Math.Clamp(seconds, 0, VisualDuration.TotalSeconds))));
    public void Clear() { Runtime = null; Snapshot = null; Audio = []; Svg = ""; Json = ""; }
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
        var scene = snapshot.SceneAt(TimeSpan.Zero);
        var audio = snapshot.RenderAudio();
        var svg = TemporalSvgRenderer.Render(scene);
        var json = TemporalVisualJson.Serialize(scene);
        Snapshot = snapshot; Audio = audio; Svg = svg; Json = json;
    }
}
