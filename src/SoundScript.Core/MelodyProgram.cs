namespace SoundScript.Core;

public sealed class MelodyProgram
{
    public int Bpm { get; set; } = 120;
    public List<ParsedNote> Notes { get; } = [];
}
