using SoundScript.Compose;
using SoundScript.Core.Notation;
using SoundScript.Wordbank;

namespace SoundScript.Timbre;

/// <summary>
/// Maps phoneme symbols to <see cref="TimbreProfile"/> values loaded from the
/// wordbank locale pack, with optional SoundCSS overrides.
/// </summary>
public static class PhonemeTimbreMapper
{
    private static readonly object CacheLock = new();
    private static string? _cachedLocaleCode;
    private static TimbreProfile? _cachedDefault;
    private static Dictionary<string, TimbreProfile>? _cachedTable;

    /// <summary>Fallback profile for phonemes without an explicit row.</summary>
    public static TimbreProfile DefaultProfile
    {
        get
        {
            EnsureCache();
            return _cachedDefault!;
        }
    }

    private static Dictionary<string, TimbreProfile> BuiltInTable
    {
        get
        {
            EnsureCache();
            return _cachedTable!;
        }
    }

    private static void EnsureCache()
    {
        var code = WordbankCatalog.ActiveLocaleCode;
        if (_cachedLocaleCode == code && _cachedTable is not null && _cachedDefault is not null)
            return;

        lock (CacheLock)
        {
            code = WordbankCatalog.ActiveLocaleCode;
            if (_cachedLocaleCode == code && _cachedTable is not null && _cachedDefault is not null)
                return;

            _cachedLocaleCode = code;
            _cachedDefault = WordbankTimbreMappings.ToProfile(WordbankCatalog.Active.PhonemeTimbre.Default);
            var table = new Dictionary<string, TimbreProfile>(StringComparer.Ordinal);
            foreach (var (phoneme, row) in WordbankCatalog.Active.TimbreProfileMap)
                table[phoneme] = WordbankTimbreMappings.ToProfile(row);
            _cachedTable = table;
        }
    }

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
}
