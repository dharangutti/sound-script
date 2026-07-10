namespace SoundScript.Wordbank.Models;

public sealed class PhonemeTimbreDocument
{
    public int Version { get; init; }
    public string Locale { get; init; } = "";
    public TimbreProfileRow Default { get; init; } = new();
    public TimbreProfileRow[] Phonemes { get; init; } = [];
}

public sealed class TimbreProfileRow
{
    public string? Phoneme { get; init; }
    public double BurstMs { get; init; }
    public double Noise { get; init; }
    public double Brightness { get; init; }
    public double Formant1Hz { get; init; }
    public double Formant2Hz { get; init; }
    public double Formant3Hz { get; init; }
    public double Formant1BwHz { get; init; }
    public double Formant2BwHz { get; init; }
    public double Formant3BwHz { get; init; }
    public double Smoothness { get; init; }
    public double Nasal { get; init; }
    public double Openness { get; init; }
    public double Harmonic1 { get; init; }
    public double Harmonic2 { get; init; }
    public double Harmonic3 { get; init; }
    public double NoiseFricative { get; init; }
    public double NoisePlosive { get; init; }
    public double TransientMs { get; init; }
    public string HarmonicRolloff { get; init; } = "default";
    public double FormantQ { get; init; }
    public double NoiseBandHz { get; init; }
    public double FrameSmoothing { get; init; }
}
