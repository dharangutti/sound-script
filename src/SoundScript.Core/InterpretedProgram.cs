namespace SoundScript.Core;

public sealed class InterpretedProgram
{
    public int Tempo { get; set; } = 120;
    public TempoAutomationMap TempoMap { get; } = new();
    public int? TimeSignatureNumerator { get; set; }
    public int? TimeSignatureDenominator { get; set; }
    public List<InterpretedTrack> Tracks { get; } = [];
    public List<InterpretedVocalTrack> VocalTracks { get; } = [];
    public List<string> Warnings { get; } = [];
}
