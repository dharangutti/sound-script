using System.Text.Json;
using System.Text.Json.Serialization;
using IndustrialMonitoring;
using SoundScript;
using SoundScript.Core;
using SoundScript.Media;
using SoundScript.Parser;
using SoundScript.Visual;

if (args.Length > 3 || args is ["--help"])
{
    Console.WriteLine("Usage: IndustrialMonitoring [all|scenario.json] [output-directory] [ffmpeg-executable]");
    return args is ["--help"] ? 0 : 2;
}
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
json.Converters.Add(new JsonStringEnumConverter<EquipmentStatus>(allowIntegerValues: false));
try
{
    string selection = args.ElementAtOrDefault(0) ?? "all";
    string output = args.ElementAtOrDefault(1) ?? "artifacts/samples/industrial-monitoring";
    string ffmpeg = args.ElementAtOrDefault(2) ?? Environment.GetEnvironmentVariable("SOUNDSCRIPT_FFMPEG") ?? "ffmpeg";
    string[] inputs = selection == "all"
        ? new[] { "healthy", "warning", "critical" }.Select(name => Path.Combine(AppContext.BaseDirectory, "Scenarios", name + ".json")).ToArray()
        : [selection];
    // Validate every input before creating outputs.
    var scenarios = inputs.Select(path => JsonSerializer.Deserialize<MonitoringScenario>(File.ReadAllText(path), json)
        ?? throw new InvalidDataException($"Empty scenario: {path}")).ToArray();
    foreach (var scenario in scenarios) scenario.Validate();
    bool videoAvailable = true;
    try { FfmpegWebmExporter.EnsureCapabilities(ffmpeg); }
    catch (DependencyException ex) { videoAvailable = false; Console.WriteLine($"WebM skipped: {ex.Message}"); }

    foreach (var scenario in scenarios)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        string name = scenario.Status.ToString().ToLowerInvariant();
        string directory = Path.Combine(output, name);
        Directory.CreateDirectory(directory);
        var sources = MonitoringSourceBuilder.Build(scenario);
        void Save(string suffix, byte[] bytes) => File.WriteAllBytes(Path.Combine(directory, name + suffix), bytes);
        File.WriteAllText(Path.Combine(directory, name + ".ss"), sources.Music);
        File.WriteAllText(Path.Combine(directory, name + ".ssw"), sources.Wave);
        File.WriteAllText(Path.Combine(directory, name + ".ssv"), sources.Visual);
        var music = SoundScriptEngine.Compile(sources.Music);
        Save(".mid", music.RenderMidi());
        Save("-music.wav", music.RenderWave());
        Save("-stereo.wav", music.RenderStereoWave());
        Save("-alert.wav", SoundScriptEngine.Compile(sources.Wave).RenderWave());

        var program = new Parser(new Tokenizer(sources.Visual).Tokenize()).Parse();
        var timeline = VisualInterpreter.Interpret(program);
        var states = new[] { 0.0, 2.0, 3.99 }.Select(t => timeline.StateAt(TimeSpan.FromSeconds(t))).ToArray();
        var scenes = states.Select(TemporalVisualSceneBuilder.Build).ToArray();
        File.WriteAllText(Path.Combine(directory, "timeline.json"), JsonSerializer.Serialize(new { timeline.Duration, States = states }, json));
        File.WriteAllText(Path.Combine(directory, "scenes.json"), JsonSerializer.Serialize(scenes, json));
        string audio = Path.Combine(directory, name + "-synchronized.wav");
        File.WriteAllBytes(audio, TemporalAudioRenderer.RenderToWavBytes(program, timeline.Duration));
        string webm = Path.Combine(directory, name + ".webm");
        // Do not leave a previous scenario's video looking like the current successful result.
        if (File.Exists(webm)) File.Delete(webm);
        if (videoAvailable)
        {
            string frames = Path.Combine(Path.GetTempPath(), "soundscript-monitoring-" + Guid.NewGuid().ToString("N"));
            try
            {
                var plan = TemporalVisualSceneBuilder.Build(TemporalVideoExportPlanBuilder.Build(timeline, new(24)));
                TemporalVideoFrameRenderer.WritePpmFrames(plan, frames, 640, 360,
                    cancellationToken: cancellation.Token, jobs: 2);
                FfmpegWebmExporter.EncodeAndVerify(ffmpeg, frames, audio, webm, plan, cancellation.Token);
            }
            finally { if (Directory.Exists(frames)) Directory.Delete(frames, recursive: true); }
        }
        File.WriteAllText(Path.Combine(directory, "scenario.json"), JsonSerializer.Serialize(new
        {
            Scenario = scenario, sources.Tempo, sources.NoteCount,
            WebM = videoAvailable ? "decode-verified" : "skipped: FFmpeg unavailable or missing codecs"
        }, json));
        Console.WriteLine($"{scenario.Status}: {sources.Tempo} BPM, {sources.NoteCount} notes; {Path.GetFullPath(directory)}");
    }
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled."); return 130; }
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
