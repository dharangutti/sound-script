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

    /// <summary>Parses a SoundCSS source string into phoneme profile overrides.</summary>
    public static IReadOnlyDictionary<string, TimbreProfileOverrides> ParseOverrides(string source)
    {
        var profiles = new Dictionary<string, TimbreProfileOverrides>(StringComparer.Ordinal);
        string? selector = null;
        var builder = new ProfileBuilder();

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
                var open = line.IndexOf('{');
                var close = line.LastIndexOf('}');
                var singleSelector = line[..open].Trim();
                var body = line[(open + 1)..close].Trim();
                Flush(selector, builder, profiles);
                selector = singleSelector;
                builder = new ProfileBuilder();
                foreach (var declaration in body.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    ParseDeclaration(declaration, selector, ref builder);
                Flush(selector, builder, profiles);
                selector = null;
                builder = new ProfileBuilder();
                continue;
            }

            if (line.EndsWith('{'))
            {
                Flush(selector, builder, profiles);
                selector = line[..^1].Trim();
                builder = new ProfileBuilder();
                continue;
            }

            if (line is "}")
            {
                Flush(selector, builder, profiles);
                selector = null;
                builder = new ProfileBuilder();
                continue;
            }

            if (selector is null)
                throw new FormatException($"Property outside selector block: {line}");

            ParseDeclaration(line, selector, ref builder);
        }

        Flush(selector, builder, profiles);
        return profiles;
    }

    private static void ParseDeclaration(string line, string selector, ref ProfileBuilder builder)
    {
            var colon = line.IndexOf(':');
            if (colon < 0)
                throw new FormatException($"Expected property: value; got '{line}'");

            var property = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().TrimEnd(';');
            builder.Apply(property, value);
    }

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

    private static void Flush(
        string? selector,
        ProfileBuilder builder,
        IDictionary<string, TimbreProfileOverrides> profiles)
    {
        if (selector is null)
            return;

        profiles[selector] = builder.Build();
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
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

        public void Apply(string property, string rawValue)
        {
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
                Openness = _openness
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
