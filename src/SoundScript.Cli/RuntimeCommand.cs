using System.Globalization;
using SoundScript.Media;

namespace SoundScript.Cli;

public static partial class CommandHandlers
{
    private static int Runtime(CliArguments args)
    {
        var permitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "out", "runtime", "param", "params", "json", "at", "verbose" };
        if (args.Options.Keys.FirstOrDefault(k => !permitted.Contains(k)) is { } unsupported)
            throw new CliUsageException($"Runtime snapshots do not support --{unsupported}; use the ordinary command for that workflow.");
        var assignments = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var value in args.Values("param"))
        {
            var parts = value.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !decimal.TryParse(parts[1],
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                throw new CliUsageException("--param requires name=decimal, for example --param intensity=0.8.");
            if (!assignments.TryAdd(parts[0], number)) throw new CliUsageException($"Duplicate --param '{parts[0]}'.");
        }
        var runtime = SoundScriptEngine.CompileRuntimeFile(args.Input);
        try { runtime.SetMany(assignments); }
        catch (ArgumentException ex) { throw new CliUsageException(ex.Message); }
        var snapshot = runtime.Bind();
        if (args.Command == "inspect")
        {
            var parameters = runtime.Parameters.Select(p => new { p.Name, Type = "decimal", p.Default, p.Minimum, p.Maximum, Value = runtime.Get(p.Name) }).ToArray();
            var scene = args.Has("at") ? snapshot.SceneAt(CliArguments.ParseTime(args.Value("at")!)) : null;
            if (args.Has("json")) Diagnostics.WriteResult(args.Command, args.Input, [], results: new { parameters, scene });
            else
            {
                foreach (var p in parameters) Console.WriteLine(FormattableString.Invariant($"{p.Name}: decimal = {p.Value} (default {p.Default}, range {p.Minimum}..{p.Maximum})"));
                if (scene is not null) Console.WriteLine(TemporalVisualJson.Serialize(scene));
            }
            return 0;
        }
        var output = args.Value("out") ?? (args.Command == "run" ? "output.mid" : "output.wav");
        var extension = args.Command == "run" ? ".mid" : ".wav";
        if (!output.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) throw new CliUsageException($"Runtime {args.Command} output requires {extension}.");
        WriteMedia(output, temporary => File.WriteAllBytes(temporary, args.Command == "run" ? snapshot.RenderMidi() : snapshot.RenderWave()));
        Console.WriteLine($"Rendered runtime snapshot to {output}.");
        return 0;
    }
}
