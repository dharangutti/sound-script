using System.Globalization;
using System.Text;

namespace SoundScript.Timbre;

/// <summary>
/// Parses SoundCSS (<c>.ssc</c>) files into phoneme → <see cref="TimbreProfile"/>
/// mappings. Deterministic, pure data — no randomness, no side effects.
/// </summary>
public static class SoundCSSParser
{
    /// <summary>Optional phoneme sequence declared at the top of a stylesheet.</summary>
    public static IReadOnlyList<string>? ParsePhonemeSequence(string source)
    {
        foreach (var rawLine in source.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (!line.StartsWith("@phonemes", StringComparison.Ordinal))
                continue;

            var values = line["@phonemes".Length..].Trim().TrimEnd(';').Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);
            return values.Length == 0 ? null : values;
        }

        return null;
    }

    /// <summary>
    /// Parses a SoundCSS source string into phoneme profile overrides. Word rules
    /// (quoted selectors, e.g. <c>"hello" { ... }</c>) are ignored here so existing
    /// phoneme stylesheets keep parsing unchanged.
    /// </summary>
    public static IReadOnlyDictionary<string, TimbreProfileOverrides> ParseOverrides(string source)
    {
        var profiles = new Dictionary<string, TimbreProfileOverrides>(StringComparer.Ordinal);

        foreach (var block in EnumerateBlocks(source))
        {
            if (IsWordSelector(block.Selector))
                continue;

            var builder = new ProfileBuilder();
            foreach (var declaration in block.Declarations)
                builder.Apply(declaration.Property, declaration.Value);

            profiles[block.Selector] = builder.Build();
        }

        return profiles;
    }

    /// <summary>
    /// Parses word-level pronunciation rules (quoted selectors) into validated
    /// <see cref="SoundCssPronunciation"/> objects keyed by word (case-insensitive).
    /// Phoneme selector blocks are ignored. Later duplicate word rules win.
    /// </summary>
    public static IReadOnlyDictionary<string, SoundCssPronunciation> ParsePronunciations(string source)
    {
        var result = new Dictionary<string, SoundCssPronunciation>(StringComparer.OrdinalIgnoreCase);

        foreach (var block in EnumerateBlocks(source))
        {
            if (!IsWordSelector(block.Selector))
                continue;

            var word = ExtractWord(block.Selector);
            if (word.Length == 0)
                throw new FormatException("Word rule selector must contain a non-empty quoted word.");

            var builder = new PronunciationBuilder(word);
            foreach (var declaration in block.Declarations)
                builder.Apply(declaration.Property, declaration.Value);

            result[word] = builder.Build();
        }

        return result;
    }

    /// <summary>
    /// Parses word rules into deterministic <see cref="TransformPlan"/>s for the
    /// rendering pipeline, keyed by word (case-insensitive).
    /// </summary>
    public static IReadOnlyDictionary<string, TransformPlan> ParseTransformPlans(string source)
    {
        var pronunciations = ParsePronunciations(source);
        var plans = new Dictionary<string, TransformPlan>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pronunciations)
            plans[pair.Key] = pair.Value.ToTransformPlan();

        return plans;
    }

    private static bool IsWordSelector(string selector) =>
        selector.Length >= 2 && selector[0] == '"' && selector[^1] == '"';

    private static string ExtractWord(string selector) => selector[1..^1].Trim();

    private readonly record struct Declaration(string Property, string Value);

    private sealed record Block(string Selector, IReadOnlyList<Declaration> Declarations);

    /// <summary>
    /// Tokenizes SoundCSS into selector blocks (single-line and multi-line),
    /// skipping <c>@</c> directives. Shared by phoneme and word-rule parsing so
    /// both agree on block boundaries.
    /// </summary>
    private static IEnumerable<Block> EnumerateBlocks(string source)
    {
        var blocks = new List<Block>();
        string? selector = null;
        List<Declaration>? declarations = null;

        foreach (var rawLine in source.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('@'))
                continue;

            // single-line block:  p { burst: 12ms; noise: 0.3; }
            if (line.Contains('{') && line.Contains('}'))
            {
                if (selector is not null)
                    blocks.Add(new Block(selector, declarations!));

                var open = line.IndexOf('{');
                var close = line.LastIndexOf('}');
                var singleSelector = line[..open].Trim();
                var body = line[(open + 1)..close].Trim();

                var singleDeclarations = new List<Declaration>();
                foreach (var declaration in body.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    singleDeclarations.Add(ParseDeclaration(declaration));

                blocks.Add(new Block(singleSelector, singleDeclarations));
                selector = null;
                declarations = null;
                continue;
            }

            if (line.EndsWith('{'))
            {
                if (selector is not null)
                    blocks.Add(new Block(selector, declarations!));

                selector = line[..^1].Trim();
                declarations = new List<Declaration>();
                continue;
            }

            if (line is "}")
            {
                if (selector is not null)
                    blocks.Add(new Block(selector, declarations!));

                selector = null;
                declarations = null;
                continue;
            }

            if (selector is null)
                throw new FormatException($"Property outside selector block: {line}");

            declarations!.Add(ParseDeclaration(line));
        }

        if (selector is not null)
            blocks.Add(new Block(selector, declarations!));

        return blocks;
    }

    private static Declaration ParseDeclaration(string line)
    {
        var colon = line.IndexOf(':');
        if (colon < 0)
            throw new FormatException($"Expected property: value; got '{line}'");

        var property = line[..colon].Trim();
        var value = line[(colon + 1)..].Trim().TrimEnd(';');
        return new Declaration(property, value);
    }

    private static HarmonicRolloffCurve ParseRolloffCurve(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "exp" or "exponential" => HarmonicRolloffCurve.Exponential,
        "linear" => HarmonicRolloffCurve.Linear,
        "polynomial" or "poly" => HarmonicRolloffCurve.Polynomial,
        "default" or "none" => HarmonicRolloffCurve.Default,
        _ => throw new FormatException($"Unknown harmonic-rolloff value '{raw}'.")
    };

    /// <summary>Loads and parses a SoundCSS file from disk.</summary>
    public static IReadOnlyDictionary<string, TimbreProfile> ParseFile(string path)
    {
        var source = File.ReadAllText(path, Encoding.UTF8);
        return Parse(source);
    }

    /// <summary>Parses a SoundCSS source string into resolved phoneme profiles.</summary>
    public static IReadOnlyDictionary<string, TimbreProfile> Parse(string source)
    {
        var overrides = ParseOverrides(source);
        return overrides.ToDictionary(
            pair => pair.Key,
            pair => TimbreProfile.ApplyOverrides(TimbreProfile.Default, pair.Value),
            StringComparer.Ordinal);
    }

    /// <summary>Loads and parses a SoundCSS file from disk.</summary>
    public static IReadOnlyDictionary<string, TimbreProfileOverrides> ParseOverridesFile(string path)
    {
        var source = File.ReadAllText(path, Encoding.UTF8);
        return ParseOverrides(source);
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }

    /// <summary>Accumulates and validates word-level pronunciation attributes.</summary>
    private sealed class PronunciationBuilder
    {
        private readonly string _word;
        private SoundCssStyle? _style;
        private SoundCssAccent? _accent;
        private SoundCssSpeed? _speed;
        private double? _pitch;
        private SoundCssEnergy? _energy;
        private SoundCssTimbre? _timbre;
        private SoundCssGender? _gender;
        private SoundCssAge? _age;
        private SoundCssPersona? _persona;
        private SoundCssEmotion? _emotion;
        private SoundCssBreath? _breath;
        private SoundCssVibrato? _vibrato;

        public PronunciationBuilder(string word) => _word = word;

        public void Apply(string property, string rawValue)
        {
            var name = property.Trim().ToLowerInvariant();
            var value = rawValue.Trim().ToLowerInvariant();

            switch (name)
            {
                case "style":
                    _style = value switch
                    {
                        "normal" => SoundCssStyle.Normal,
                        "sing" => SoundCssStyle.Sing,
                        "whisper" => SoundCssStyle.Whisper,
                        "shout" => SoundCssStyle.Shout,
                        _ => throw Invalid("style", value, "normal, sing, whisper, shout"),
                    };
                    break;
                case "accent":
                    _accent = value switch
                    {
                        "usa" => SoundCssAccent.Usa,
                        "uk" => SoundCssAccent.Uk,
                        "india" => SoundCssAccent.India,
                        _ => throw Invalid("accent", value, "usa, uk, india"),
                    };
                    break;
                case "speed":
                    _speed = ParseSpeed(value);
                    break;
                case "pitch":
                    _pitch = ParsePitch(value);
                    break;
                case "energy":
                    _energy = value switch
                    {
                        "high" => SoundCssEnergy.High,
                        "medium" => SoundCssEnergy.Medium,
                        "low" => SoundCssEnergy.Low,
                        _ => throw Invalid("energy", value, "high, medium, low"),
                    };
                    break;
                case "timbre":
                    _timbre = value switch
                    {
                        "bright" => SoundCssTimbre.Bright,
                        "dark" => SoundCssTimbre.Dark,
                        "flat" => SoundCssTimbre.Flat,
                        _ => throw Invalid("timbre", value, "bright, dark, flat"),
                    };
                    break;
                case "gender":
                    _gender = value switch
                    {
                        "male" => SoundCssGender.Male,
                        "female" => SoundCssGender.Female,
                        "neutral" => SoundCssGender.Neutral,
                        _ => throw Invalid("gender", value, "male, female, neutral"),
                    };
                    break;
                case "age":
                    _age = value switch
                    {
                        "child" => SoundCssAge.Child,
                        "teen" => SoundCssAge.Teen,
                        "adult" => SoundCssAge.Adult,
                        "senior" => SoundCssAge.Senior,
                        _ => throw Invalid("age", value, "child, teen, adult, senior"),
                    };
                    break;
                case "persona":
                    _persona = value switch
                    {
                        "narrator" => SoundCssPersona.Narrator,
                        "robot" => SoundCssPersona.Robot,
                        "soft" => SoundCssPersona.Soft,
                        "bright" => SoundCssPersona.Bright,
                        _ => throw Invalid("persona", value, "narrator, robot, soft, bright"),
                    };
                    break;
                case "emotion":
                    _emotion = value switch
                    {
                        "happy" => SoundCssEmotion.Happy,
                        "sad" => SoundCssEmotion.Sad,
                        "angry" => SoundCssEmotion.Angry,
                        "calm" => SoundCssEmotion.Calm,
                        "excited" => SoundCssEmotion.Excited,
                        _ => throw Invalid("emotion", value, "happy, sad, angry, calm, excited"),
                    };
                    break;
                case "breath":
                    _breath = value switch
                    {
                        "none" => SoundCssBreath.None,
                        "low" => SoundCssBreath.Low,
                        "medium" => SoundCssBreath.Medium,
                        "high" => SoundCssBreath.High,
                        _ => throw Invalid("breath", value, "none, low, medium, high"),
                    };
                    break;
                case "vibrato":
                    _vibrato = value switch
                    {
                        "none" => SoundCssVibrato.None,
                        "light" => SoundCssVibrato.Light,
                        "medium" => SoundCssVibrato.Medium,
                        "strong" => SoundCssVibrato.Strong,
                        _ => throw Invalid("vibrato", value, "none, light, medium, strong"),
                    };
                    break;
                default:
                    throw new FormatException(
                        $"Unknown word pronunciation attribute '{property.Trim()}'.");
            }
        }

        public SoundCssPronunciation Build() => new()
        {
            Word = _word,
            Style = _style,
            Accent = _accent,
            Speed = _speed,
            PitchSemitones = _pitch,
            Energy = _energy,
            Timbre = _timbre,
            Gender = _gender,
            Age = _age,
            Persona = _persona,
            Emotion = _emotion,
            Breath = _breath,
            Vibrato = _vibrato,
        };

        private static SoundCssSpeed ParseSpeed(string value)
        {
            if (value == "fast")
                return new SoundCssSpeed(SoundCssSpeedMode.Fast, null);
            if (value == "slow")
                return new SoundCssSpeed(SoundCssSpeedMode.Slow, null);

            if (value.StartsWith('x'))
            {
                var number = value[1..];
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier)
                    && multiplier is > 0.1 and <= 10.0)
                {
                    return new SoundCssSpeed(SoundCssSpeedMode.Explicit, multiplier);
                }
            }

            throw Invalid("speed", value, "fast, slow, xN (0.1 < N ≤ 10, e.g. x1.2, x0.8)");
        }

        private static double ParsePitch(string value)
        {
            if (!double.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var semitones))
            {
                throw Invalid("pitch", value, "+N or -N semitones (e.g. +2, -3)");
            }

            if (semitones is < -24 or > 24)
                throw new FormatException($"Pitch value '{value}' out of range (−24..24 semitones).");

            return semitones;
        }

        private static FormatException Invalid(string attribute, string value, string allowed) =>
            new($"Invalid {attribute} value '{value}'. Allowed: {allowed}.");
    }

    private sealed class ProfileBuilder
    {
        private double? _burstMs;
        private double? _noise;
        private double? _brightness;
        private double? _formant1Hz;
        private double? _formant2Hz;
        private double? _formant3Hz;
        private double? _formant1BwHz;
        private double? _formant2BwHz;
        private double? _formant3BwHz;
        private double? _smoothness;
        private double? _nasal;
        private double? _openness;
        private double? _harmonic1;
        private double? _harmonic2;
        private double? _harmonic3;
        private double? _noiseFricative;
        private double? _noisePlosive;
        private double? _transientMs;
        private HarmonicRolloffCurve? _harmonicRolloff;
        private double? _formantQ;
        private double? _noiseBandHz;
        private double? _frameSmoothing;

        public void Apply(string property, string rawValue)
        {
            if (property.Trim().ToLowerInvariant() == "harmonic-rolloff")
            {
                _harmonicRolloff = ParseRolloffCurve(rawValue);
                return;
            }

            var value = ParseValue(rawValue);
            switch (property.ToLowerInvariant())
            {
                case "burst":
                    _burstMs = value;
                    break;
                case "noise":
                    _noise = value;
                    break;
                case "brightness":
                    _brightness = value;
                    break;
                case "formant1":
                    _formant1Hz = value;
                    break;
                case "formant2":
                    _formant2Hz = value;
                    break;
                case "formant3":
                    _formant3Hz = value;
                    break;
                case "formant1bw":
                    _formant1BwHz = value;
                    break;
                case "formant2bw":
                    _formant2BwHz = value;
                    break;
                case "formant3bw":
                    _formant3BwHz = value;
                    break;
                case "smoothness":
                    _smoothness = value;
                    break;
                case "nasal":
                    _nasal = value;
                    break;
                case "openness":
                    _openness = value;
                    break;
                case "harmonic1":
                    _harmonic1 = value;
                    break;
                case "harmonic2":
                    _harmonic2 = value;
                    break;
                case "harmonic3":
                    _harmonic3 = value;
                    break;
                case "noise-fricative":
                    _noiseFricative = value;
                    break;
                case "noise-plosive":
                    _noisePlosive = value;
                    break;
                case "transient":
                    _transientMs = value;
                    break;
                case "formant-q":
                    _formantQ = value;
                    break;
                case "noise-band":
                    _noiseBandHz = value;
                    break;
                case "smoothing":
                    _frameSmoothing = value;
                    break;
                default:
                    throw new FormatException($"Unknown SoundCSS property '{property}'.");
            }
        }

        public TimbreProfileOverrides Build() =>
            new()
            {
                BurstMs = _burstMs,
                Noise = _noise,
                Brightness = _brightness,
                Formant1Hz = _formant1Hz,
                Formant2Hz = _formant2Hz,
                Formant3Hz = _formant3Hz,
                Formant1BwHz = _formant1BwHz,
                Formant2BwHz = _formant2BwHz,
                Formant3BwHz = _formant3BwHz,
                Smoothness = _smoothness,
                Nasal = _nasal,
                Openness = _openness,
                Harmonic1 = _harmonic1,
                Harmonic2 = _harmonic2,
                Harmonic3 = _harmonic3,
                NoiseFricative = _noiseFricative,
                NoisePlosive = _noisePlosive,
                TransientMs = _transientMs,
                HarmonicRolloff = _harmonicRolloff,
                FormantQ = _formantQ,
                NoiseBandHz = _noiseBandHz,
                FrameSmoothing = _frameSmoothing
            };

        private static double ParseValue(string raw)
        {
            raw = raw.Trim();
            if (raw.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
                return double.Parse(raw[..^2], CultureInfo.InvariantCulture);

            if (raw.EndsWith("hz", StringComparison.OrdinalIgnoreCase))
                return double.Parse(raw[..^2], CultureInfo.InvariantCulture);

            return double.Parse(raw, CultureInfo.InvariantCulture);
        }
    }
}
