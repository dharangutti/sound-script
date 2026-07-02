using SoundScript.Midi;
using SoundScript.Parser;

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
    var source = File.ReadAllText(scriptPath);
    var tokens = new Tokenizer(source).Tokenize();
    var program = new Parser(tokens).Parse();
    var timedNotes = Interpreter.Interpret(program);
    MidiGenerator.Write(program, timedNotes, outputPath);

    Console.WriteLine($"Wrote {timedNotes.Count} notes to {outputPath} at {program.Bpm} BPM.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
