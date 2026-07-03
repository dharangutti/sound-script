namespace SoundScript.Midi;

internal sealed class OrchestrationSettings
{
    public bool DoubleOctave { get; set; }
    public bool ReinforceBass { get; set; }
    public bool BrightenTop { get; set; }

    public bool HasAny => DoubleOctave || ReinforceBass || BrightenTop;
}
