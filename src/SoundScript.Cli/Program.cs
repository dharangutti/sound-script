using SoundScript.Compose;
using SoundScript.Midi;
using SoundScript.Parser;
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
    _ => PrintUsage()
};

static int PrintUsage()
{
    Console.Error.WriteLine("Usage: soundscript run <script.ss> [output.mid]");
    Console.Error.WriteLine("       soundscript compose \"<text>\" [output.mid]");
    return 1;
}

static string ResolveOutputPath(string[] args) =>
    args.Length > 2
        ? args[2]
        : Path.Combine(Directory.GetCurrentDirectory(), "output.mid");

static int Run(string[] args)
{
    var scriptPath = args[1];
    if (!File.Exists(scriptPath))
    {
        Console.Error.WriteLine($"Script not found: {scriptPath}");
        return 1;
    }

    var outputPath = ResolveOutputPath(args);

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

    var outputPath = ResolveOutputPath(args);

    try
    {
        var interpreted = PhonemeComposer.ComposeProgram(text);
        MidiGenerator.Write(interpreted, outputPath);

        var syllables = PhonemeComposer.SplitSyllables(text);
        var noteCount = interpreted.Tracks.Sum(t => t.Notes.Count);
        Console.WriteLine(
            $"Composed {syllables.Count} syllable(s) into {noteCount} note(s) " +
            $"to {outputPath} at {interpreted.Tempo} BPM.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}
