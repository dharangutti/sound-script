using SoundScript.Wordbank;
using SoundScript.Wordbank.Models;

namespace SoundScript.Timbre;

internal static class WordbankTimbreMappings
{
    internal static TimbreProfile ToProfile(TimbreProfileRow row)
    {
        var fallback = WordbankCatalog.Active.PhonemeTimbre.Default;
        return new TimbreProfile
        {
            BurstMs = Pick(row.BurstMs, fallback.BurstMs),
            Noise = Pick(row.Noise, fallback.Noise, 0.1),
            Brightness = Pick(row.Brightness, fallback.Brightness, 0.5),
            Formant1Hz = Pick(row.Formant1Hz, fallback.Formant1Hz, 500),
            Formant2Hz = Pick(row.Formant2Hz, fallback.Formant2Hz, 1500),
            Formant3Hz = Pick(row.Formant3Hz, fallback.Formant3Hz, 2500),
            Formant1BwHz = Pick(row.Formant1BwHz, fallback.Formant1BwHz, 80),
            Formant2BwHz = Pick(row.Formant2BwHz, fallback.Formant2BwHz, 110),
            Formant3BwHz = Pick(row.Formant3BwHz, fallback.Formant3BwHz, 150),
            Smoothness = Pick(row.Smoothness, fallback.Smoothness, 0.5),
            Nasal = row.Nasal,
            Openness = Pick(row.Openness, fallback.Openness, 0.5),
            Harmonic1 = Pick(row.Harmonic1, fallback.Harmonic1, 0.9),
            Harmonic2 = Pick(row.Harmonic2, fallback.Harmonic2, 0.5),
            Harmonic3 = Pick(row.Harmonic3, fallback.Harmonic3, 0.25),
            NoiseFricative = Pick(row.NoiseFricative, fallback.NoiseFricative, 0.1),
            NoisePlosive = Pick(row.NoisePlosive, fallback.NoisePlosive, 0.05),
            TransientMs = Pick(row.TransientMs, fallback.TransientMs, 6),
            HarmonicRolloff = ParseRolloff(
                string.IsNullOrEmpty(row.HarmonicRolloff) || row.HarmonicRolloff == "default"
                    ? fallback.HarmonicRolloff
                    : row.HarmonicRolloff),
            FormantQ = Pick(row.FormantQ, fallback.FormantQ, 1.0),
            NoiseBandHz = row.NoiseBandHz,
            FrameSmoothing = Pick(row.FrameSmoothing, fallback.FrameSmoothing, 0.2),
        };
    }

    private static double Pick(double value, double fallback, double? classDefault = null) =>
        value != 0 ? value : fallback != 0 ? fallback : classDefault ?? 0;

    private static HarmonicRolloffCurve ParseRolloff(string value) => value switch
    {
        "exponential" => HarmonicRolloffCurve.Exponential,
        "linear" => HarmonicRolloffCurve.Linear,
        "polynomial" => HarmonicRolloffCurve.Polynomial,
        _ => HarmonicRolloffCurve.Default,
    };
}
