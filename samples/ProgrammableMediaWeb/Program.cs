using ProgrammableMedia;
using SoundScript;
using SoundScript.Media;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// A finite application-owned scenario catalog. The sample never accepts arbitrary source or paths.
var catalog = Enum.GetValues<EquipmentStatus>().ToDictionary(s => s.ToString().ToLowerInvariant(),
    s => SoundScriptEngine.Compile(new MonitoringScenario(s).BuildSource()).CompileMedia());
var runtime = SoundScriptEngine.CompileRuntime("""
    param intensity = 0.25
    param xpos = 200
    perform expressive
    tempo 120
    track cue { gain intensity C4 q E4 q G4 h }
    visual "indicator" for 4s {
        shape circle
        fill "#ef4444"
        set x xpos
        set y 360
        set width 120
        set height 120
        set opacity intensity
    }
    """);
var runtimeGate = new object();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/audio", (string? scenario) =>
    catalog.TryGetValue(scenario ?? "healthy", out var media)
        ? Results.File(media.RenderAudio(), "audio/wav", enableRangeProcessing: true) : Results.BadRequest("Unknown scenario."));
app.MapGet("/api/duration", (string? scenario) =>
    catalog.TryGetValue(scenario ?? "healthy", out var media)
        ? Results.Json(new { durationSeconds = media.Duration.TotalSeconds }) : Results.BadRequest("Unknown scenario."));
IResult Scene(string? scenario, double t, bool svg)
{
    if (!catalog.TryGetValue(scenario ?? "healthy", out var media)) return Results.BadRequest("Unknown scenario.");
    if (!double.IsFinite(t) || t < 0 || t > TimeSpan.MaxValue.TotalSeconds - 1) return Results.BadRequest("Invalid media time.");
    var scene = media.SceneAt(TimeSpan.FromSeconds(t));
    return svg ? Results.Text(TemporalSvgRenderer.Render(scene), "image/svg+xml")
        : Results.Text(TemporalVisualJson.Serialize(scene), "application/json");
}
app.MapGet("/api/scene", (string? scenario, double t) => Scene(scenario, t, false));
app.MapGet("/api/scene.svg", (string? scenario, double t) => Scene(scenario, t, true));
IResult RuntimeOutput(decimal intensity, decimal xpos, double? t, bool svg)
{
    if (!double.IsFinite(t ?? 2) || (t ?? 2) < 0 || (t ?? 2) > TimeSpan.MaxValue.TotalSeconds - 1)
        return Results.BadRequest("Invalid media time.");
    try
    {
        lock (runtimeGate)
        {
            runtime.SetMany(new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["intensity"] = intensity,
                ["xpos"] = xpos
            });
            var snapshot = runtime.Bind();
            if (svg)
                return Results.Text(TemporalSvgRenderer.Render(snapshot.SceneAt(TimeSpan.FromSeconds(t ?? 2))), "image/svg+xml");
            return Results.File(snapshot.RenderAudio(), "audio/wav", enableRangeProcessing: true);
        }
    }
    catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
}
app.MapGet("/api/runtime/audio", (decimal intensity, decimal xpos) => RuntimeOutput(intensity, xpos, null, false));
app.MapGet("/api/runtime/scene.svg", (decimal intensity, decimal xpos, double? t) => RuntimeOutput(intensity, xpos, t, true));
app.MapGet("/api/runtime/info", () => Results.Json(new
{
    parameters = runtime.Parameters.Select(p => new { p.Name, p.Default, p.Minimum, p.Maximum }),
    runtime.Statistics
}));
app.Run();
