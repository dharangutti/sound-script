using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoLab;

public sealed record Parameter(decimal Default, decimal Min, decimal Max);
public sealed record Video(string Asset, int Trim, int Frames, int? At = null, int Fade = 0);
public sealed record Audio(string Asset, int Trim, int Frames, int At, string Gain);
public sealed record Shape(int At, int Frames, int Width, int Height, string Color, string X, int ToX, int Y);
public sealed record Script(int Width, int Height, int Fps, int Frames,
    ImmutableDictionary<string, Parameter> Parameters, ImmutableArray<Video> Videos,
    ImmutableArray<Audio> Audio, ImmutableArray<Shape> Shapes);
public sealed record Clip(string Asset, int Trim, int Frames, int At, int Fade);
public sealed record VisibleClip(string Asset, int SourceFrame, decimal Opacity);
public sealed record VisibleShape(decimal X, int Y, int Width, int Height, string Color);
public sealed record Scene(ImmutableArray<VisibleClip> Clips, ImmutableArray<VisibleShape> Shapes);

public sealed class Composition
{
    public Script Script { get; }
    public ImmutableArray<Clip> Clips { get; }
    public string AssetDirectory { get; }
    private Composition(Script script, ImmutableArray<Clip> clips, string directory)
        => (Script, Clips, AssetDirectory) = (script, clips, directory);

    public static Composition Compile(string source, string directory)
    {
        var s = JsonSerializer.Deserialize<Script>(source, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        }) ?? throw new ArgumentException("Empty script.");
        Require(s.Width is > 0 and <= 4096 && s.Height is > 0 and <= 4096 && s.Width % 2 == 0 && s.Height % 2 == 0, "Canvas must have even dimensions up to 4096.");
        Require(new[] {24, 25, 30, 50, 60}.Contains(s.Fps), "fps must be 24, 25, 30, 50 or 60.");
        Require(s.Frames is > 0 and <= 216000, "Invalid timeline length.");
        Require(s.Parameters != null && !s.Videos.IsDefaultOrEmpty && !s.Audio.IsDefault && !s.Shapes.IsDefault, "Collections are required; at least one video is required.");
        foreach (var (name, p) in s.Parameters!)
            Require(!string.IsNullOrWhiteSpace(name) && p != null && p.Min <= p.Default && p.Default <= p.Max, "Invalid parameter declaration.");
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
            Span(a!.At, a.Frames, a.Trim); Reference(a.Gain, 0, 4);
        }
        foreach (var o in s.Shapes)
        {
            Require(o != null, "Shape required."); Span(o!.At, o.Frames);
            Require(o.Width > 0 && o.Height > 0 && o.Width % 2 == 0 && o.Height % 2 == 0 && o.Width <= s.Width && o.Height <= s.Height && o.Y >= 0 && o.Y + (long)o.Height <= s.Height && o.ToX >= 0 && o.ToX + (long)o.Width <= s.Width, "Shape must have even dimensions and stay inside canvas.");
            Require(System.Text.RegularExpressions.Regex.IsMatch(o.Color ?? "", "^[0-9a-fA-F]{6}$"), "Color must be six hex digits.");
            Reference(o.X, 0, s.Width - o.Width);
        }
        return new(s, clips.ToImmutable(), Path.GetFullPath(directory));
    }
    internal static void Require(bool condition, string message)
    { if (!condition) throw new ArgumentException(message); }
    public Runtime CreateRuntime() => new(this);
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
        return new(
            Composition.Clips.Where(c => frame >= c.At && frame < c.At + c.Frames)
                .Select(c => new VisibleClip(c.Asset, c.Trim + frame - c.At, c.Fade == 0 ? 1 : Math.Min(1m, (decimal)(frame - c.At) / c.Fade))).ToImmutableArray(),
            Composition.Script.Shapes.Where(o => frame >= o.At && frame < o.At + o.Frames)
                .Select(o => new VisibleShape(Values[o.X] + (o.ToX - Values[o.X]) * (frame - o.At) / Math.Max(1, o.Frames - 1), o.Y, o.Width, o.Height, o.Color)).ToImmutableArray());
    }
}
