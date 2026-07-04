using SoundScript.Compose;
using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Timbre;
using SoundScript.Voice;

if (args.Length < 2)
{
    PrintUsage();
    return 1;
}

return args[0].ToLowerInvariant() switch
{
    "run" => Run(args),
    "compose" => Compose(args),
    "render" => Render(args),
    _ => PrintUsage()
};

static int PrintUsage()
{
    Console.Error.WriteLine("Usage: soundscript run <script.ss> [output.mid]");
    Console.Error.WriteLine("       soundscript compose \"<text>\" [output.mid] [--append <script.ss>]");
    Console.Error.WriteLine("       soundscript render <file.mid> --css <style.ssc> --out <output.wav|ogg> [--text \"<source text>\"]");
    return 1;
}

static int Run(string[] args)
{
    var scriptPath = args[1];
    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 1;
    }

    var outputPath = args.Length > 2
        ? args[2]
        : Path.Combine(Directory.GetCurrentDirectory(), "output.mid");

    try
    {
        var loaded = ProgramLoader.Load(scriptPath);
        var interpreted = Interpreter.Interpret(loaded.Program, scriptPath);
        VocalInterpreter.Apply(loaded.Program, interpreted);
        foreach (var warning in loaded.Warnings)
            interpreted.Warnings.Add(warning);

        MidiGenerator.Write(interpreted, outputPath);

        foreach (var warning in interpreted.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        var summary = $"Wrote {noteCount} notes across {interpreted.Tracks.Count} track(s)";
        if (interpreted.VocalTracks.Count > 0)
        {
            var syllableCount = interpreted.VocalTracks.Sum(v => v.Syllables.Count);
            summary += $" and {syllableCount} sung syllable(s) across {interpreted.VocalTracks.Count} voice(s)";
        }

        Console.WriteLine($"{summary} to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Compose(string[] args)
{
    var text = args[1];
    if (string.IsNullOrWhiteSpace(text))
    {
        Console.Error.WriteLine("Nothing to compose: the text is empty.");
        return 1;
    }

    string? outputPath = null;
    string? appendScriptPath = null;

    for (var i = 2; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--append", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--append requires a script path: compose \"text\" --append file.ss");
                return 1;
            }

            appendScriptPath = args[++i];
        }
        else
        {
            outputPath = args[i];
        }
    }

    outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output.mid");

    try
    {
        SoundScript.Core.InterpretedProgram interpreted;
        if (appendScriptPath is not null)
        {
            if (!File.Exists(appendScriptPath))
            {
                Console.Error.WriteLine($"Script not found: {appendScriptPath}");
                return 1;
            }

            var loaded = ProgramLoader.Load(appendScriptPath);
            interpreted = Interpreter.Interpret(loaded.Program, appendScriptPath);
            VocalInterpreter.Apply(loaded.Program, interpreted);
            foreach (var warning in loaded.Warnings)
                interpreted.Warnings.Add(warning);

            PhonemeComposer.AppendTo(interpreted, text);
        }
        else
        {
            interpreted = PhonemeComposer.ComposeProgram(text);
        }

        MidiGenerator.Write(interpreted, outputPath);

        foreach (var warning in interpreted.Warnings)
            Console.Error.WriteLine($"warning: {warning}");

        var syllables = PhonemeComposer.SplitSyllables(text);
        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        var summary = appendScriptPath is not null
            ? $"Composed {syllables.Count} syllable(s) and appended the phoneme track to {appendScriptPath}: " +
              $"{noteCount} note(s) across {interpreted.Tracks.Count} track(s)"
            : $"Composed {syllables.Count} syllable(s) into {noteCount} note(s)";
        Console.WriteLine($"{summary} to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int Render(string[] args)
{
    var midiPath = args[1];
    if (!File.Exists(midiPath))
    {
        Console.Error.WriteLine($"MIDI not found: {midiPath}");
        return 1;
    }

    string? cssPath = null;
    string? outputPath = null;
    string? sourceText = null;

    for (var i = 2; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--css", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--css requires a SoundCSS file path.");
                return 1;
            }

            cssPath = args[++i];
        }
        else if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--out requires an output audio path.");
                return 1;
            }

            outputPath = args[++i];
        }
        else if (string.Equals(args[i], "--text", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("--text requires the source plain text for phoneme alignment.");
                return 1;
            }

            sourceText = args[++i];
        }
        else
        {
            Console.Error.WriteLine($"Unknown render argument: {args[i]}");
            return 1;
        }
    }

    if (cssPath is null || !File.Exists(cssPath))
    {
        Console.Error.WriteLine("Render requires --css <style.ssc> pointing to an existing SoundCSS file.");
        return 1;
    }

    outputPath ??= Path.Combine(Directory.GetCurrentDirectory(), "output.wav");

    try
    {
        var options = new OfflineRenderer.RenderOptions { SourceText = sourceText };
        OfflineRenderer.RenderFile(midiPath, cssPath, outputPath, options);

        Console.WriteLine($"Rendered {midiPath} with {cssPath} to {outputPath}.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}
