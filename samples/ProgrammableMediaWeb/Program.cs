using ProgrammableMedia;
using SoundScript;
using SoundScript.Media;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// A finite application-owned scenario catalog. The sample never accepts arbitrary source or paths.
var catalog = Enum.GetValues<EquipmentStatus>().ToDictionary(s => s.ToString().ToLowerInvariant(),
    s => SoundScriptEngine.Compile(new MonitoringScenario(s).BuildSource()).CompileMedia());
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
app.Run();
