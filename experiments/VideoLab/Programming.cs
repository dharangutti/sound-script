using System.Collections.Immutable;
using System.Text.Json;

namespace VideoLab;

public sealed record Crop(decimal X, decimal Y, decimal Width, decimal Height);
public sealed record RasterTransform(int Width, int Height, int RotatedWidth, int RotatedHeight, int Left, int Top);
public sealed record Transform(decimal X, decimal Y, decimal Width, decimal Height, decimal ScaleX,
    decimal ScaleY, decimal Rotation, decimal Opacity, decimal AnchorX, decimal AnchorY, Crop Crop)
{
    public RasterTransform Raster()
    {
        int w = Math.Max(1, (int)Math.Round(Width * ScaleX, MidpointRounding.AwayFromZero));
        int h = Math.Max(1, (int)Math.Round(Height * ScaleY, MidpointRounding.AwayFromZero));
        double angle = (double)Rotation * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
        int rw = (int)Math.Ceiling(Math.Abs(cos) * w + Math.Abs(sin) * h - 1e-9);
        int rh = (int)Math.Ceiling(Math.Abs(sin) * w + Math.Abs(cos) * h - 1e-9);
        double dx = (0.5 - (double)AnchorX) * w, dy = (0.5 - (double)AnchorY) * h;
        int left = (int)Math.Round((double)X + cos * dx - sin * dy - rw / 2.0, MidpointRounding.AwayFromZero);
        int top = (int)Math.Round((double)Y + sin * dx + cos * dy - rh / 2.0, MidpointRounding.AwayFromZero);
        return new(w, h, rw, rh, left, top);
    }
}
public sealed record LayerState(string Id, string Kind, string Source, int? SourceFrame, int ZOrder, bool Included,
    Transform Transform, ImmutableDictionary<string, decimal> Outputs);
public sealed record AudioState(string Id, string Source, int SourceFrame, decimal Gain, bool Included);
public sealed record EffectTemplate(ImmutableDictionary<string, decimal> Defaults, ImmutableDictionary<string, Scalar> Transform);
public sealed record VisualProgram(string Id, string Kind, string Source, int Trim, int At, int Frames, int Fade,
    int ZOrder, int BaseWidth, int BaseHeight, ImmutableDictionary<string, Scalar> Properties, Expression? Condition)
{
    public LayerState Evaluate(EvaluationContext context)
    {
        try
        {
            var p = Properties.ToImmutableDictionary(k => k.Key, k => k.Value.Evaluate(context), StringComparer.Ordinal);
            decimal V(string key) => p[key];
            Composition.Require(V("width") > 0 && V("height") > 0 && V("width") <= 4096 && V("height") <= 4096, "Invalid dimensions.");
            Composition.Require(V("scaleX") > 0 && V("scaleY") > 0 && V("width") * V("scaleX") <= 4096 && V("height") * V("scaleY") <= 4096, "Scale must be positive; scaled dimensions cannot exceed 4096.");
            Composition.Require(V("opacity") >= 0 && V("opacity") <= 1, "Opacity outside [0,1].");
            Composition.Require(V("anchorX") >= 0 && V("anchorX") <= 1 && V("anchorY") >= 0 && V("anchorY") <= 1, "Anchor outside [0,1].");
            Composition.Require(Math.Abs(V("x")) <= 16384 && Math.Abs(V("y")) <= 16384 && Math.Abs(V("rotation")) <= 360000, "Position or rotation exceeds limits.");
            var crop = new Crop(V("cropX"), V("cropY"), V("cropWidth"), V("cropHeight"));
            Composition.Require(crop.X >= 0 && crop.Y >= 0 && crop.Width > 0 && crop.Height > 0 && crop.X + crop.Width <= 1 && crop.Y + crop.Height <= 1 && crop.Width * BaseWidth >= 1 && crop.Height * BaseHeight >= 1, "Invalid normalized crop rectangle.");
            decimal opacity = V("opacity") * (Fade == 0 ? 1 : Math.Min(1m, (decimal)context.Frame / Fade));
            return new(Id, Kind, Source, Kind == "video" ? checked(Trim + context.Frame) : null, ZOrder,
                Condition?.Evaluate(context).Boolean ?? true,
                new(V("x"), V("y"), V("width"), V("height"), V("scaleX"), V("scaleY"), V("rotation"), opacity, V("anchorX"), V("anchorY"), crop), p);
        }
        catch (Exception e) when (e is ArgumentException or OverflowException)
        { throw new ArgumentException($"{Id}, local frame {context.Frame}: {e.Message}", e); }
    }
}
public sealed record AudioProgram(string Id, string Asset, int Trim, int At, int Frames, Scalar Gain, Expression? Condition)
{
    public AudioState Evaluate(EvaluationContext context)
    {
        try
        {
            var gain = Gain.Evaluate(context);
            Composition.Require(gain >= 0 && gain <= 4, "Audio gain outside [0,4].");
            return new(Id, Asset, checked(Trim + context.Frame), gain, Condition?.Evaluate(context).Boolean ?? true);
        }
        catch (Exception e) when (e is ArgumentException or OverflowException)
        { throw new ArgumentException($"{Id}, local frame {context.Frame}: {e.Message}", e); }
    }
}

internal static class Programming
{
    internal static readonly string[] PropertyNames = ["x", "y", "width", "height", "scale", "scaleX", "scaleY", "rotation", "opacity", "anchorX", "anchorY", "cropX", "cropY", "cropWidth", "cropHeight"];
    internal static Script Expand(Script script)
    {
        Composition.Require(!script.Videos.IsDefault && !script.Audio.IsDefault && !script.Shapes.IsDefault, "videos, audio and shapes arrays are required.");
        Composition.Require(script.Videos.All(v => v != null) && script.Audio.All(a => a != null) && script.Shapes.All(s => s != null), "Null elements are not allowed.");
        int count = 0;
        foreach (var (name, records) in script.Data ?? ImmutableDictionary<string, ImmutableArray<DataRecord>>.Empty)
        {
            Composition.Require(!string.IsNullOrWhiteSpace(name) && !records.IsDefault && records.All(r => r != null && !string.IsNullOrWhiteSpace(r.Asset) && r.Frames > 0 && r.Trim >= 0), $"Invalid data '{name}'.");
            count += records.Length;
        }
        Composition.Require(count <= 128 && (script.Data?.Count ?? 0) <= 32, "Limit: 128 records in 32 data sets.");
        var videos = script.Videos.ToBuilder(); int generated = 0;
        if (!script.Sequences.IsDefault)
        {
            Composition.Require(script.Sequences.Length <= 32, "Limit: 32 generated sequences.");
            foreach (var sequence in script.Sequences)
            {
                Composition.Require(sequence != null && script.Data != null && script.Data.ContainsKey(sequence.Data), "Sequence references missing data.");
                int at = sequence!.At;
                foreach (var record in script.Data![sequence.Data])
                {
                    Composition.Require(++generated <= 128, "Limit: 128 generated elements.");
                    int fade = at == sequence.At ? 0 : sequence.Fade;
                    videos.Add(new(record.Asset, record.Trim, record.Frames, at - fade, fade, sequence.Transform, Effects: sequence.Effects));
                    at = checked(at - fade + record.Frames);
                }
            }
        }
        return script with { Videos = videos.ToImmutable() };
    }

    internal static (ImmutableArray<VisualProgram>, ImmutableArray<AudioProgram>, ImmutableDictionary<string, EffectTemplate>, bool) Compile(Script s, ImmutableArray<Clip> clips)
    {
        Composition.Require((s.Effects?.Count ?? 0) <= 32, "Limit: 32 effects; effect nesting is forbidden.");
        var templates = ImmutableDictionary.CreateBuilder<string, EffectTemplate>(StringComparer.Ordinal);
        foreach (var (name, definition) in (s.Effects ?? ImmutableDictionary<string, EffectDefinition>.Empty).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            try
            {
                Composition.Require(definition != null && definition.Parameters != null && definition.Transform != null && definition.Parameters.Count <= 16, "Invalid effect definition or more than 16 arguments.");
                foreach (var local in definition!.Parameters.Keys)
                    Composition.Require(System.Text.RegularExpressions.Regex.IsMatch(local, "^[A-Za-z_][A-Za-z0-9_]*$") && !Expressions.Builtins.Contains(local) && !s.Parameters.ContainsKey(local), "Effect locals cannot shadow globals or built-ins.");
                var values = ImmutableDictionary.CreateBuilder<string, Scalar>(StringComparer.Ordinal);
                foreach (var (property, value) in definition.Transform.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    Composition.Require(PropertyNames.Contains(property), $"Unknown transform '{property}'.");
                    values.Add(property, Scalar.Compile(value, 216000, s.Parameters.Keys.Concat(definition.Parameters.Keys)));
                }
                templates.Add(name, new(definition.Parameters, values.ToImmutable()));
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException)
            { throw new ArgumentException($"Effect definition '{name}': {e.Message}", e); }
        }
        bool advanced = s.Effects?.Count > 0 || !s.Sequences.IsDefaultOrEmpty;
        Expression? Condition(string? expression)
        {
            if (expression == null) return null;
            var result = Expressions.Parse(expression, s.Parameters.Keys);
            Composition.Require(result.Kind == ValueKind.Boolean, "Condition must be a comparison returning boolean."); return result;
        }
        ImmutableDictionary<string, Scalar> Properties(int width, int height, int frames, ImmutableDictionary<string, JsonElement>? transform, ImmutableArray<EffectUse> uses, Shape? legacy)
        {
            var props = new Dictionary<string, Scalar>(StringComparer.Ordinal)
            {
                ["x"] = Scalar.Fixed(0), ["y"] = Scalar.Fixed(legacy?.Y ?? 0), ["width"] = Scalar.Fixed(width), ["height"] = Scalar.Fixed(height),
                ["scaleX"] = Scalar.Fixed(1), ["scaleY"] = Scalar.Fixed(1), ["rotation"] = Scalar.Fixed(0), ["opacity"] = Scalar.Fixed(1),
                ["anchorX"] = Scalar.Fixed(0), ["anchorY"] = Scalar.Fixed(0), ["cropX"] = Scalar.Fixed(0), ["cropY"] = Scalar.Fixed(0), ["cropWidth"] = Scalar.Fixed(1), ["cropHeight"] = Scalar.Fixed(1)
            };
            if (legacy?.X != null)
                props["x"] = frames == 1 ? new ExpressionScalar(new Variable(legacy.X)) : new Animation([
                    new(0, new Variable(legacy.X), InterpolationKind.Linear), new(frames - 1, new Literal(legacy.ToX!.Value), InterpolationKind.Linear)]);
            void Put(string key, Scalar value)
            {
                Composition.Require(PropertyNames.Contains(key), $"Unknown transform '{key}'.");
                if (value is Animation animation) Composition.Require(animation.Keys[^1].Frame < frames, "Effect keyframe exceeds instance lifetime.");
                if (key == "scale") { props["scaleX"] = value; props["scaleY"] = value; } else props[key] = value;
            }
            if (!uses.IsDefault)
            {
                Composition.Require(uses.Length <= 8, "Limit: 8 effects per instance.");
                foreach (var use in uses)
                {
                    try
                    {
                        Composition.Require(use != null && templates.ContainsKey(use.Name), "Unknown effect.");
                        var template = templates[use!.Name];
                        var aliases = template.Defaults.ToDictionary(p => p.Key, p => (Expression)new Literal(p.Value), StringComparer.Ordinal);
                        foreach (var (key, value) in use.Arguments ?? ImmutableDictionary<string, JsonElement>.Empty)
                        {
                            Composition.Require(aliases.ContainsKey(key), $"Unknown argument '{key}'.");
                            aliases[key] = Scalar.ReadExpression(value, s.Parameters.Keys, null);
                        }
                        foreach (var (key, value) in template.Transform.OrderBy(p => p.Key, StringComparer.Ordinal)) Put(key, Substitute(value, aliases));
                    }
                    catch (Exception e) when (e is ArgumentException or KeyNotFoundException or InvalidOperationException)
                    { throw new ArgumentException($"Effect invocation '{use?.Name}': {e.Message}", e); }
                }
            }
            foreach (var (key, value) in (transform ?? ImmutableDictionary<string, JsonElement>.Empty).OrderBy(p => p.Key, StringComparer.Ordinal)) Put(key, Scalar.Compile(value, frames, s.Parameters.Keys));
            return props.ToImmutableDictionary(StringComparer.Ordinal);
        }
        var visuals = ImmutableArray.CreateBuilder<VisualProgram>();
        for (int i = 0; i < clips.Length; i++)
        {
            var c = clips[i]; var v = s.Videos[i];
            advanced |= v.Transform != null || v.When != null || !v.Effects.IsDefaultOrEmpty;
            visuals.Add(new($"video[{i}]", "video", c.Asset, c.Trim, c.At, c.Frames, c.Fade, i, s.Width, s.Height,
                Properties(s.Width, s.Height, c.Frames, v.Transform, v.Effects, null), Condition(v.When)));
        }
        for (int i = 0; i < s.Shapes.Length; i++)
        {
            var v = s.Shapes[i]; advanced |= v.Transform != null || v.When != null || !v.Effects.IsDefaultOrEmpty || v.X == null;
            visuals.Add(new($"shape[{i}]", "shape", v.Color, 0, v.At, v.Frames, 0, clips.Length + i, v.Width, v.Height,
                Properties(v.Width, v.Height, v.Frames, v.Transform, v.Effects, v), Condition(v.When)));
        }
        var audio = ImmutableArray.CreateBuilder<AudioProgram>();
        for (int i = 0; i < s.Audio.Length; i++)
        {
            var a = s.Audio[i];
            advanced |= a.When != null || a.Gain.ValueKind != JsonValueKind.String || !s.Parameters.ContainsKey(a.Gain.GetString()!);
            audio.Add(new($"audio[{i}]", a.Asset, a.Trim, a.At, a.Frames, Scalar.Compile(a.Gain, a.Frames, s.Parameters.Keys), Condition(a.When)));
        }
        if (advanced)
        {
            Composition.Require(s.Frames <= 600, "Programmable lowering is limited to 600 frames.");
            Composition.Require(visuals.Sum(v => (long)v.Frames) + audio.Sum(a => (long)a.Frames) <= 4096, "Limit: 4096 evaluated element-frames.");
            Composition.Require((long)s.Width * s.Height * s.Frames <= 100_000_000, "Limit: 100 million canvas pixels per programmable render.");
        }
        return (visuals.ToImmutable(), audio.ToImmutable(), templates.ToImmutable(), advanced);
    }
    private static Scalar Substitute(Scalar scalar, IReadOnlyDictionary<string, Expression> aliases)
    {
        Expression Visit(Expression e) => e switch
        {
            Variable v when aliases.TryGetValue(v.Name, out var replacement) => replacement,
            Unary u => new Unary(Visit(u.Operand)), Binary b => b with { Left = Visit(b.Left), Right = Visit(b.Right) },
            Function f => f with { Arguments = f.Arguments.Select(Visit).ToImmutableArray() }, _ => e
        };
        Expression Checked(Expression e) { var result = Visit(e); Expressions.ValidateSize(result); return result; }
        return scalar switch
        {
            ExpressionScalar e => new ExpressionScalar(Checked(e.Expression)),
            Animation a => new Animation(a.Keys.Select(k => k with { Value = Checked(k.Value) }).ToImmutableArray()),
            _ => throw new InvalidOperationException()
        };
    }
}
