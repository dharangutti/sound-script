using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoLab;

public sealed record Parameter(decimal Default, decimal Min, decimal Max);
public sealed record EffectUse(string Name, ImmutableDictionary<string, JsonElement>? Arguments = null);
public sealed record EffectDefinition(ImmutableDictionary<string, decimal> Parameters, ImmutableDictionary<string, JsonElement> Transform);
public sealed record DataRecord(string Asset, int Frames, int Trim = 0);
public sealed record Sequence(string Data, int At, int Fade = 0, ImmutableDictionary<string, JsonElement>? Transform = null, ImmutableArray<EffectUse> Effects = default);
public sealed record Video(string Asset, int Trim, int Frames, int? At = null, int Fade = 0,
    ImmutableDictionary<string, JsonElement>? Transform = null, string? When = null, ImmutableArray<EffectUse> Effects = default);
public sealed record Audio(string Asset, int Trim, int Frames, int At, JsonElement Gain, string? When = null);
public sealed record Shape(int At, int Frames, int Width, int Height, string Color, string? X = null, int? ToX = null, int Y = 0,
    ImmutableDictionary<string, JsonElement>? Transform = null, string? When = null, ImmutableArray<EffectUse> Effects = default);
public sealed record Script(int Width, int Height, int Fps, int Frames,
    ImmutableDictionary<string, Parameter> Parameters, ImmutableArray<Video> Videos,
    ImmutableArray<Audio> Audio, ImmutableArray<Shape> Shapes,
    ImmutableDictionary<string, EffectDefinition>? Effects = null,
    ImmutableDictionary<string, ImmutableArray<DataRecord>>? Data = null, ImmutableArray<Sequence> Sequences = default);
public sealed record Clip(string Asset, int Trim, int Frames, int At, int Fade);
public sealed record VisibleClip(string Asset, int SourceFrame, decimal Opacity);
public sealed record VisibleShape(decimal X, int Y, int Width, int Height, string Color);
public sealed record Scene(ImmutableArray<VisibleClip> Clips, ImmutableArray<VisibleShape> Shapes,
    ImmutableArray<LayerState> Layers, ImmutableArray<AudioState> Audio);

public sealed class Composition
{
    public Script Script { get; }
    public ImmutableArray<Clip> Clips { get; }
    public string AssetDirectory { get; }
    public ImmutableArray<VisualProgram> Visuals { get; }
    public ImmutableArray<AudioProgram> AudioPrograms { get; }
    public ImmutableDictionary<string, EffectTemplate> Effects { get; }
    public bool Programmable { get; }
    private Composition(Script script, ImmutableArray<Clip> clips, string directory)
    {
        (Script, Clips, AssetDirectory) = (script, clips, directory);
        (Visuals, AudioPrograms, Effects, Programmable) = Programming.Compile(script, clips);
    }

    public static Composition Compile(string source, string directory)
    {
        try { return CompileCore(source, directory); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or OverflowException or FormatException)
        { throw new ArgumentException($"Invalid composition: {e.Message}", e); }
    }
    private static Composition CompileCore(string source, string directory)
    {
        Require(source.Length <= 1_000_000, "Script exceeds one million characters.");
        using var document = JsonDocument.Parse(source, new JsonDocumentOptions { MaxDepth = 48 });
        JsonRules.NoDuplicates(document.RootElement);
        var s = JsonSerializer.Deserialize<Script>(source, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        }) ?? throw new ArgumentException("Empty script.");
        s = Programming.Expand(s);
        Require(s.Width is > 0 and <= 4096 && s.Height is > 0 and <= 4096 && s.Width % 2 == 0 && s.Height % 2 == 0, "Canvas must have even dimensions up to 4096.");
        Require(new[] {24, 25, 30, 50, 60}.Contains(s.Fps), "fps must be 24, 25, 30, 50 or 60.");
        Require(s.Frames is > 0 and <= 216000, "Invalid timeline length.");
        Require(s.Parameters != null && !s.Videos.IsDefault && !s.Audio.IsDefault && !s.Shapes.IsDefault && s.Videos.Length + s.Shapes.Length > 0, "Collections and at least one visual are required.");
        Require(s.Parameters!.Count <= 64 && s.Videos.Length + s.Shapes.Length + s.Audio.Length <= 64, "Limit: 64 parameters and 64 total elements.");
        Require(s.Videos.Select(v => v.Asset).Concat(s.Audio.Select(a => a.Asset)).Distinct(StringComparer.Ordinal).Count() <= 64, "Limit: 64 assets.");
        foreach (var (name, p) in s.Parameters!)
            Require(System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$") && !Expressions.Builtins.Contains(name) && p != null && p.Min <= p.Default && p.Default <= p.Max, "Invalid or reserved parameter declaration.");
        void Reference(string name, decimal min, decimal max)
        {
            Require(s.Parameters.TryGetValue(name, out var p) && p.Min >= min && p.Max <= max, $"Parameter '{name}' must be declared within [{min}, {max}].");
        }
        void Span(int at, int frames, int trim = 0) => Require(at >= 0 && frames > 0 && trim >= 0 && (long)at + frames <= s.Frames, "Span outside timeline.");
        var clips = ImmutableArray.CreateBuilder<Clip>();
        int end = 0;
        foreach (var v in s.Videos)
        {
            Require(v != null && !string.IsNullOrWhiteSpace(v.Asset), "Video asset required.");
            var at = v!.At ?? end - v.Fade;
            Span(at, v.Frames, v.Trim);
            Require(v.Fade >= 0 && v.Fade < v.Frames, "Invalid fade length.");
            Require(clips.Count == 0 ? v.Fade == 0 : v.Fade == 0 ? at >= end : at == end - v.Fade && v.Fade < clips[^1].Frames && at >= clips[^1].At + clips[^1].Fade,
                "Clips must be ordered; overlaps must exactly match the incoming fade and cannot overlap another transition.");
            clips.Add(new(v.Asset, v.Trim, v.Frames, at, v.Fade));
            end = at + v.Frames;
        }
        foreach (var a in s.Audio)
        {
            Require(a != null && !string.IsNullOrWhiteSpace(a.Asset), "Audio asset required.");
            Span(a!.At, a.Frames, a.Trim);
            if (a.Gain.ValueKind == JsonValueKind.String && s.Parameters.ContainsKey(a.Gain.GetString()!)) Reference(a.Gain.GetString()!, 0, 4);
        }
        foreach (var o in s.Shapes)
        {
            Require(o != null, "Shape required."); Span(o!.At, o.Frames);
            Require(o.Width > 0 && o.Height > 0 && o.Width % 2 == 0 && o.Height % 2 == 0 && o.Width <= s.Width && o.Height <= s.Height, "Shape must have positive even dimensions within the canvas.");
            Require(System.Text.RegularExpressions.Regex.IsMatch(o.Color ?? "", "^[0-9a-fA-F]{6}$"), "Color must be six hex digits.");
            if (o.X != null)
            {
                Require(o.ToX != null && o.Y >= 0 && o.Y + (long)o.Height <= s.Height && o.ToX >= 0 && o.ToX + (long)o.Width <= s.Width, "Legacy shape outside canvas.");
                Reference(o.X, 0, s.Width - o.Width);
            }
            else Require(o.ToX == null, "toX requires a legacy x parameter.");
        }
        var result = new Composition(s, clips.ToImmutable(), Path.GetFullPath(directory));
        result.ValidateValues(s.Parameters.ToImmutableDictionary(p => p.Key, p => p.Value.Default));
        return result;
    }
    internal static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
    { if (!condition) throw new ArgumentException(message); }
    public Runtime CreateRuntime() => new(this);
    internal void ValidateValues(ImmutableDictionary<string, decimal> values)
    {
        if (!Programmable) return;
        var snapshot = new Snapshot(this, values);
        long pixels = 0;
        for (int frame = 0; frame < Script.Frames; frame++)
            foreach (var layer in snapshot.SceneAt(frame).Layers)
            {
                var raster = layer.Transform.Raster();
                pixels += (long)raster.Width * raster.Height;
                Require(pixels <= 100_000_000, "Transform work exceeds 100 million layer pixels.");
            }
    }
}

public sealed class Runtime
{
    private readonly Composition composition;
    private ImmutableDictionary<string, decimal> values;
    private readonly object gate = new();
    internal Runtime(Composition c) { composition = c; values = c.Script.Parameters.ToImmutableDictionary(p => p.Key, p => p.Value.Default); }
    public void SetMany(IReadOnlyDictionary<string, decimal> changes)
    {
        lock (gate)
        {
            var next = values;
            foreach (var (name, value) in changes)
            {
                Composition.Require(composition.Script.Parameters.TryGetValue(name, out var p) && value >= p.Min && value <= p.Max, $"Unknown or out-of-range parameter '{name}'.");
                next = next.SetItem(name, value);
            }
            composition.ValidateValues(next);
            values = next;
        }
    }
    public Snapshot Bind() { lock (gate) return new(composition, values); }
}

public sealed class Snapshot
{
    public Composition Composition { get; }
    public ImmutableDictionary<string, decimal> Values { get; }
    internal Snapshot(Composition composition, ImmutableDictionary<string, decimal> values)
        => (Composition, Values) = (composition, values);
    public Scene SceneAt(int frame)
    {
        Composition.Require(frame >= 0 && frame < Composition.Script.Frames, "Frame outside timeline.");
        var layers = Composition.Visuals.Where(v => frame >= v.At && frame < v.At + v.Frames)
            .Select(v => v.Evaluate(Context(frame - v.At, v.Frames))).ToImmutableArray();
        var audio = Composition.AudioPrograms.Where(a => frame >= a.At && frame < a.At + a.Frames)
            .Select(a => a.Evaluate(Context(frame - a.At, a.Frames))).ToImmutableArray();
        return new(
            layers.Where(l => l.Kind == "video" && l.Included).Select(l => new VisibleClip(l.Source, l.SourceFrame!.Value, l.Transform.Opacity)).ToImmutableArray(),
            layers.Where(l => l.Kind == "shape" && l.Included).Select(l => new VisibleShape(l.Transform.X, (int)l.Transform.Y, (int)l.Transform.Width, (int)l.Transform.Height, l.Source)).ToImmutableArray(), layers, audio);
    }
    private EvaluationContext Context(int frame, int frames) => new(Values, frame, frames, Composition.Script.Fps, Composition.Script.Width, Composition.Script.Height);
}
