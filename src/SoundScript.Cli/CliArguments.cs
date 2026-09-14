using System.Globalization;

namespace SoundScript.Cli;

public sealed class CliUsageException(string message) : Exception(message);

public sealed record CommandDefinition(string Name, string Usage, string Example, string Values = "", string Flags = "", int Min = 1, int Max = 1, bool PositionalOutput = false);

/// <summary>One grammar for every CLI command. Execution never reparses tokens.</summary>
public sealed class CliArguments
{
    public static readonly CommandDefinition[] Commands =
    [
        new("run", "<file.ss> [output.mid] [--out <file>]", "run song.ss --out song.mid", "out", PositionalOutput: true),
        new("compose", "<text> [output] [--wave] [--stereo] [--append <file>] [--emit-ss <file>]", "compose \"hello world\" --wave --out hello.wav", "out append emit-ss wordbank-dir locale", "wave stereo", PositionalOutput: true),
        new("prosody", "<text> [output] [--wave] [--stereo] [--append <file>] [--emit-ss <file>]", "prosody \"hello world\" --out hello.mid", "out append emit-ss wordbank-dir locale", "wave stereo", PositionalOutput: true),
        new("render", "<file.mid> --css <style.ssc> [--out <file.wav|ogg>] [--text <text>]", "render song.mid --css style.ssc --out song.wav", "out css text", PositionalOutput: true),
        new("wave", "<file.ss|ssw> [output.wav] [--out <file>] [options]", "wave song.ss --out song.wav --stereo", "out vocal vocal-at vocal-gain tts-dir offline-tts-dir offline-tts-voice css wordbank-dir locale voice seed", "stereo continuous offline-tts", PositionalOutput: true),
        new("visual", "<file.ss|ssv> [--at <seconds>]...", "visual scene.ssv --at 1.5", "at"),
        new("video", "<file.ss|ssv> [output.webm] --out <file.webm> [--check] [options]", "video scene.ssv --out scene.webm --check", "out fps width height ffmpeg", "check json", PositionalOutput: true),
        new("validate", "<file.ss|ssw|ssv|ssc> [--json] [--target midi|wave|video] [options]", "validate scene.ssv --json", "target out fps width height ffmpeg", "json"),
        new("inspect", "<file.ss|ssw|ssv|ssc> [--json] [--at <seconds>]", "inspect scene.ssv --json --at 1.5", "at", "json"),
        new("vocal generate", "<text> [--out <file.wav>] [options]", "vocal generate \"hello\" --out hello.wav --engine wordbank", "out wordbank-dir engine locale voice seed css", "continuous"),
        new("vocal batch", "<file.ss|ssw> --out-dir <folder> [options]", "vocal batch song.ssw --out-dir stems", "out-dir wordbank-dir engine locale voice seed css", "continuous skip-existing"),
        new("wordbank ensure", "<lemma> [--auto-generate-missing] [options]", "wordbank ensure hello --locale en", "locale wordbank-dir voice", "auto-generate-missing"),
        new("wordbank normalize", "[lemma | --all] [options]", "wordbank normalize --all --locale en", "locale wordbank-dir", "all", Min: 0)
    ];

    public string Command { get; private set; } = "";
    public List<string> Positionals { get; } = [];
    public Dictionary<string, List<string>> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Input => Positionals.FirstOrDefault() ?? "";
    public bool Has(string name) => Options.ContainsKey(name.TrimStart('-'));
    public string? Value(string name) => Options.GetValueOrDefault(name.TrimStart('-'))?.LastOrDefault();
    public IEnumerable<string> Values(string name) => Options.GetValueOrDefault(name.TrimStart('-')) ?? [];
    public int Integer(string name, int fallback) => Value(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;
    public double Number(string name, double fallback) => Value(name) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public static CliArguments Parse(string[] tokens)
    {
        if (tokens.Length == 0) throw new CliUsageException("A command is required. Use soundscript --help.");
        var result = new CliArguments { Command = tokens[0].ToLowerInvariant() };
        int start = 1;
        if (result.Command is "vocal" or "wordbank")
        {
            if (tokens.Length < 2) throw new CliUsageException($"{result.Command} requires a subcommand. Use soundscript {result.Command} --help.");
            result.Command += " " + tokens[1].ToLowerInvariant();
            start++;
        }
        var definition = Commands.FirstOrDefault(c => c.Name == result.Command)
            ?? throw new CliUsageException($"Unknown command '{result.Command}'. Use soundscript --help.");
        var values = definition.Values.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var flags = definition.Flags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        bool positionalOnly = false;
        for (int i = start; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token == "--" && !positionalOnly) { positionalOnly = true; continue; }
            if (positionalOnly || !token.StartsWith('-') || token == "-") { result.Positionals.Add(token); continue; }
            var parts = token.Split('=', 2);
            var key = parts[0].ToLowerInvariant() switch { "-o" or "--output" => "out", var name => name.StartsWith("--") ? name[2..] : name };
            if (!values.Contains(key) && !flags.Contains(key)) throw new CliUsageException($"Unknown option '{parts[0]}' for {result.Command}.");
            if (result.Has(key) && !(result.Command == "visual" && key == "at")) throw new CliUsageException($"Option --{key} may only be supplied once (including aliases).");
            string value = "true";
            if (values.Contains(key))
            {
                if (parts.Length == 2) value = parts[1];
                else if (i + 1 < tokens.Length && (!tokens[i + 1].StartsWith('-') || double.TryParse(tokens[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))) value = tokens[++i];
                else throw new CliUsageException($"Option --{key} requires a value.");
                if (string.IsNullOrWhiteSpace(value)) throw new CliUsageException($"Option --{key} requires a non-empty value.");
            }
            else if (key == "offline-tts")
            {
                value = parts.Length == 2 ? parts[1] : i + 1 < tokens.Length && IsEngine(tokens[i + 1]) ? tokens[++i] : "wordbank";
                if (!IsEngine(value)) throw new CliUsageException("--offline-tts requires wordbank, composite, espeak, or prosody.");
            }
            else if (parts.Length == 2) throw new CliUsageException($"Flag --{key} does not take a value.");
            if (!result.Options.TryGetValue(key, out var entries)) result.Options[key] = entries = [];
            entries.Add(value);
        }
        if (definition.PositionalOutput && result.Positionals.Count > definition.Max)
        {
            if (result.Has("out")) throw new CliUsageException("Positional output and --out/--output/-o cannot be combined.");
            result.Options["out"] = [result.Positionals[^1]];
            result.Positionals.RemoveAt(result.Positionals.Count - 1);
        }
        if (result.Positionals.Count < definition.Min || result.Positionals.Count > definition.Max)
            throw new CliUsageException($"Usage: soundscript {definition.Name} {definition.Usage}");
        result.Validate();
        return result;
    }

    private static bool IsEngine(string value) => value is "wordbank" or "composite" or "espeak" or "prosody";

    private void Validate()
    {
        foreach (var name in new[] { "fps", "width", "height", "seed" })
            if (Value(name) is { } value && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) throw new CliUsageException($"--{name} requires an integer.");
        foreach (var name in new[] { "vocal-at", "vocal-gain" })
            if (Value(name) is { } value && (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0 || name == "vocal-gain" && number > 1)) throw new CliUsageException($"Invalid --{name}: {value}.");
        foreach (var value in Values("at")) ParseTime(value);
        if (Has("fps") && Integer("fps", 30) is not (24 or 30 or 60)) throw new CliUsageException("--fps supports 24, 30, or 60.");
        foreach (var name in new[] { "width", "height" })
            if (Has(name) && (Integer(name, 0) < 2 || Integer(name, 0) % 2 != 0)) throw new CliUsageException($"--{name} requires a positive even pixel count.");
        if (Has("append") && Has("wave")) throw new CliUsageException("--wave and --append cannot be combined.");
        if (Has("append") && Has("emit-ss")) throw new CliUsageException("--emit-ss and --append cannot be combined.");
        if (Command == "render" && !Has("css")) throw new CliUsageException("render requires --css <style.ssc>.");
        if (Command == "vocal batch" && !Has("out-dir")) throw new CliUsageException("vocal batch requires --out-dir <folder>.");
        if (Command == "wordbank normalize" && (Has("all") == (Positionals.Count > 0))) throw new CliUsageException("Supply one lemma or --all.");
        if (Command == "video" && !Has("out")) throw new CliUsageException("video requires --out <clip.webm>.");
        if (Value("target") is { } target && target is not ("midi" or "wave" or "video")) throw new CliUsageException("--target supports midi, wave, or video.");
        if (Command == "validate" && Value("target") != "video" && new[] { "out", "fps", "width", "height", "ffmpeg" }.Any(Has)) throw new CliUsageException("Export options require --target video.");
        if (Command == "video" && Has("json") && !Has("check")) throw new CliUsageException("video --json requires --check.");
        if ((Command == "video" || Value("target") == "video") && Value("out") is { } path && !path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)) throw new CliUsageException("Video output must use the .webm extension.");
        if (Value("engine") is { } engine && !IsEngine(engine)) throw new CliUsageException("--engine requires wordbank, composite, espeak, or prosody.");
    }

    public static TimeSpan ParseTime(string value)
    {
        var text = value.EndsWith('s') ? value[..^1] : value;
        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds < 0 || seconds >= TimeSpan.MaxValue.TotalSeconds)
            throw new CliUsageException($"Invalid time '{value}'; use non-negative seconds such as 1.5.");
        return TimeSpan.FromSeconds(seconds);
    }

    public static void PrintHelp(string? command)
    {
        var matches = Commands.Where(c => command is null || c.Name == command || c.Name.StartsWith(command + " ")).ToArray();
        if (matches.Length == 0) throw new CliUsageException($"Unknown command '{command}'.");
        Console.WriteLine("SoundScript — deterministic audio and temporal media");
        foreach (var c in matches)
        {
            Console.WriteLine($"  soundscript {c.Name} {c.Usage}");
            if (command is not null)
            {
                Console.WriteLine("    Options: " + string.Join(" ", c.Values.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => $"--{v} <value>").Concat(c.Flags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(f => $"--{f}"))));
                Console.WriteLine($"    Example: soundscript {c.Example}");
            }
        }
        Console.WriteLine("Use --help or help <command>; --version prints the release number. --out aliases: -o, --output.");
    }
}
