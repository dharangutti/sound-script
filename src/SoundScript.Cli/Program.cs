using SoundScript.Midi;
using SoundScript.Parser;
using SoundScript.Voice;

if (args.Length < 2 || !string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: soundscript run <script.ss> [output.mid]");
    return 1;
}

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
