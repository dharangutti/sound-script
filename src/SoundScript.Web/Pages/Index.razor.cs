using SoundScript.Midi;
using SoundScript.Parser;

namespace SoundScript.Web.Pages;

public partial class Index
{
    private const string MelodyExample =
        """
        melody {
            bpm 120
            C4 E4 G4 | C5
        }
        """;

    private const string DurationsExample =
        """
        melody {
            bpm 100
            C4 for 2
            E4 for 1
            G4:4
            C5
        }
        """;

    private string ScriptText { get; set; } = MelodyExample;

    private byte[]? MidiBytes { get; set; }
    private string? ErrorMessage { get; set; }

    private void LoadMelodyExample()
    {
        ScriptText = MelodyExample;
        MidiBytes = null;
        ErrorMessage = null;
    }

    private void LoadDurationsExample()
    {
        ScriptText = DurationsExample;
        MidiBytes = null;
        ErrorMessage = null;
    }

    private void Generate()
    {
        try
        {
            ErrorMessage = null;
            MidiBytes = null;

            var tokens = new Tokenizer(ScriptText).Tokenize();
            var program = new SoundScript.Parser.Parser(tokens).Parse();
            var timedNotes = Interpreter.Interpret(program);

            using var stream = new MemoryStream();
            MidiGenerator.Write(program, timedNotes, stream);
            MidiBytes = stream.ToArray();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
