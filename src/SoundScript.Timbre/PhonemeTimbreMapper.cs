using SoundScript.Compose;
using SoundScript.Core.Notation;

namespace SoundScript.Timbre;

/// <summary>
/// Maps phoneme symbols to <see cref="TimbreProfile"/> values. Provides a
/// deterministic built-in table and merges optional SoundCSS overrides.
/// Unknown phonemes fall back to <see cref="DefaultProfile"/>.
/// </summary>
public static class PhonemeTimbreMapper
{
    /// <summary>Fallback profile for phonemes without an explicit row.</summary>
    public static TimbreProfile DefaultProfile { get; } = new()
    {
        BurstMs = 6,
        Noise = 0.15,
        Brightness = 0.45,
        Formant1Hz = 550,
        Formant2Hz = 1400,
        Formant3Hz = 2400,
        Smoothness = 0.6,
        Nasal = 0.1,
        Openness = 0.5
    };

    private static readonly Dictionary<string, TimbreProfile> BuiltInTable = new(StringComparer.Ordinal)
    {
        // plosives — short burst, low voicing
        ["p"] = Profile(burst: 14, noise: 0.35, brightness: 0.15, f1: 200, f2: 900, openness: 0.1),
        ["t"] = Profile(burst: 12, noise: 0.32, brightness: 0.25, f1: 220, f2: 1700, openness: 0.15),
        ["k"] = Profile(burst: 11, noise: 0.30, brightness: 0.30, f1: 240, f2: 2100, openness: 0.2),
        ["b"] = Profile(burst: 13, noise: 0.34, brightness: 0.12, f1: 210, f2: 850, openness: 0.12),
        ["d"] = Profile(burst: 12, noise: 0.31, brightness: 0.22, f1: 230, f2: 1600, openness: 0.14),
        ["g"] = Profile(burst: 11, noise: 0.29, brightness: 0.28, f1: 250, f2: 2000, openness: 0.18),
        ["ch"] = Profile(burst: 10, noise: 0.45, brightness: 0.55, f1: 260, f2: 2200, openness: 0.2),

        // nasals
        ["m"] = Profile(noise: 0.08, nasal: 0.85, f1: 280, f2: 1000, smoothness: 0.85, openness: 0.25),
        ["n"] = Profile(noise: 0.10, nasal: 0.80, f1: 300, f2: 1400, smoothness: 0.82, openness: 0.3),
        ["ng"] = Profile(noise: 0.12, nasal: 0.88, f1: 320, f2: 1800, smoothness: 0.8, openness: 0.35),

        // fricatives — noise-heavy
        ["s"] = Profile(burst: 4, noise: 0.82, brightness: 0.9, f1: 180, f2: 4800, openness: 0.2),
        ["sh"] = Profile(burst: 4, noise: 0.78, brightness: 0.75, f1: 200, f2: 3200, openness: 0.25),
        ["th"] = Profile(burst: 5, noise: 0.75, brightness: 0.7, f1: 190, f2: 3600, openness: 0.22),
        ["f"] = Profile(burst: 5, noise: 0.72, brightness: 0.65, f1: 210, f2: 2800, openness: 0.18),
        ["v"] = Profile(burst: 5, noise: 0.68, brightness: 0.55, f1: 220, f2: 2400, openness: 0.2),
        ["z"] = Profile(burst: 4, noise: 0.76, brightness: 0.72, f1: 200, f2: 4200, openness: 0.22),
        ["h"] = Profile(burst: 3, noise: 0.55, brightness: 0.5, f1: 500, f2: 1500, openness: 0.4),

        // liquids / glides
        ["r"] = Profile(noise: 0.18, brightness: 0.55, f1: 400, f2: 1200, smoothness: 0.7, openness: 0.45),
        ["l"] = Profile(noise: 0.14, brightness: 0.48, f1: 380, f2: 1100, smoothness: 0.72, openness: 0.42),
        ["w"] = Profile(noise: 0.12, brightness: 0.35, f1: 300, f2: 800, smoothness: 0.88, openness: 0.3),
        ["j"] = Profile(noise: 0.16, brightness: 0.5, f1: 320, f2: 2300, smoothness: 0.8, openness: 0.35),

        // vowels
        ["aa"] = Profile(noise: 0.05, brightness: 0.42, f1: 700, f2: 1100, smoothness: 0.9, openness: 0.95),
        ["ee"] = Profile(noise: 0.05, brightness: 0.58, f1: 280, f2: 2300, smoothness: 0.88, openness: 0.25),
        ["oo"] = Profile(noise: 0.06, brightness: 0.32, f1: 320, f2: 800, smoothness: 0.9, openness: 0.3),
        ["ai"] = Profile(noise: 0.06, brightness: 0.5, f1: 650, f2: 1800, smoothness: 0.86, openness: 0.75),
        ["au"] = Profile(noise: 0.06, brightness: 0.45, f1: 600, f2: 1000, smoothness: 0.87, openness: 0.8),
    };

    /// <summary>Resolves a phoneme to its timbre profile with optional CSS overrides.</summary>
    public static TimbreProfile Map(string phoneme, IReadOnlyDictionary<string, TimbreProfileOverrides>? overrides = null)
    {
        var baseline = BuiltInTable.TryGetValue(phoneme, out var builtIn) ? builtIn : DefaultProfile;
        if (overrides is not null && overrides.TryGetValue(phoneme, out var custom))
            return TimbreProfile.ApplyOverrides(baseline, custom);

        return baseline;
    }

    /// <summary>
    /// Derives the ordered phoneme list for a plain-text input using the
    /// existing deterministic compose phonetics pipeline (read-only).
    /// </summary>
    public static IReadOnlyList<string> PhonemesFromText(string text)
    {
        var phonemes = new List<string>();
        foreach (var syllable in PhonemeComposer.SplitSyllables(text))
            phonemes.AddRange(PhonemeSplitter.Split(syllable));
        return phonemes;
    }

    /// <summary>
    /// Best-effort phoneme guess from a MIDI note signature when no text is
    /// available. Deterministic tie-breaking uses ordinal phoneme order.
    /// </summary>
    public static string GuessPhoneme(int midiNumber, double durationBeats, int velocity)
    {
        var matches = new List<string>();
        foreach (var phoneme in BuiltInTable.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            if (!MatchesSignature(phoneme, midiNumber, durationBeats, velocity))
                continue;
            matches.Add(phoneme);
        }

        return matches.Count > 0 ? matches[0] : "unknown";
    }

    private static bool MatchesSignature(string phoneme, int midiNumber, double durationBeats, int velocity)
    {
        var gesture = PhonemeMapper.Map(phoneme);
        var expectedMidi = ToMidiNumber(gesture.Pitch, gesture.Octave);
        var expectedBeats = gesture.Duration.ToBeats();
        var expectedVelocity = ExpectedVelocity(gesture.Kind);

        return midiNumber == expectedMidi
            && Math.Abs(durationBeats - expectedBeats) < 0.001
            && velocity == expectedVelocity;
    }

    private static int ExpectedVelocity(GestureKind kind) => kind switch
    {
        GestureKind.Swell => 58,
        GestureKind.Fade => 52,
        _ => 64
    };

    private static int ToMidiNumber(PitchClass pitch, int octave)
    {
        var semitone = pitch switch
        {
            PitchClass.C => 0,
            PitchClass.D => 2,
            PitchClass.E => 4,
            PitchClass.F => 5,
            PitchClass.G => 7,
            PitchClass.A => 9,
            PitchClass.B => 11,
            _ => 0
        };
        return (octave + 1) * 12 + semitone;
    }

    private static TimbreProfile Profile(
        double burst = 0,
        double noise = 0.1,
        double brightness = 0.5,
        double f1 = 500,
        double f2 = 1500,
        double f3 = 2500,
        double smoothness = 0.6,
        double nasal = 0,
        double openness = 0.5) =>
        new()
        {
            BurstMs = burst,
            Noise = noise,
            Brightness = brightness,
            Formant1Hz = f1,
            Formant2Hz = f2,
            Formant3Hz = f3,
            Smoothness = smoothness,
            Nasal = nasal,
            Openness = openness
        };
}
