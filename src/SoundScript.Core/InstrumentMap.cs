namespace SoundScript.Core;

/// <summary>General MIDI Level 1 program map with readable aliases.</summary>
public static class InstrumentMap
{
    public const int DefaultProgram = 0;
    private static readonly string[] GeneralMidiNames =
    [
        "acousticgrand", "brightacoustic", "electricgrand", "honkytonk", "electricpiano1", "electricpiano2", "harpsichord", "clavinet",
        "celesta", "glockenspiel", "musicbox", "vibraphone", "marimba", "xylophone", "tubularbells", "dulcimer",
        "drawbarorgan", "percussiveorgan", "rockorgan", "churchorgan", "reedorgan", "accordion", "harmonica", "tangoaccordion",
        "acousticguitar_nylon", "acousticguitar_steel", "electricguitar_jazz", "electricguitar_clean", "electricguitar_muted", "overdrivenguitar", "distortionguitar", "guitarharmonics",
        "acousticbass", "electricbass_finger", "electricbass_pick", "fretlessbass", "slapbass1", "slapbass2", "synthbass1", "synthbass2",
        "violin", "viola", "cello", "contrabass", "tremolostrings", "pizzicatostrings", "orchestralharp", "timpani",
        "stringensemble1", "stringensemble2", "synthstrings1", "synthstrings2", "choiraahs", "voiceoohs", "synthvoice", "orchestra_hit",
        "trumpet", "trombone", "tuba", "mutedtrumpet", "frenchhorn", "brasssection", "synthbrass1", "synthbrass2",
        "sopranosax", "altosax", "tenorsax", "baritonesax", "oboe", "englishhorn", "bassoon", "clarinet",
        "piccolo", "flute", "recorder", "panflute", "blownbottle", "shakuhachi", "whistle", "ocarina",
        "lead_square", "lead_sawtooth", "lead_calliope", "lead_chiff", "lead_charang", "lead_voice", "lead_fifths", "lead_bass_lead",
        "pad_newage", "pad_warm", "pad_polysynth", "pad_choir", "pad_bowed", "pad_metallic", "pad_halo", "pad_sweep",
        "fx_rain", "fx_soundtrack", "fx_crystal", "fx_atmosphere", "fx_brightness", "fx_goblins", "fx_echoes", "fx_scifi",
        "sitar", "banjo", "shamisen", "koto", "kalimba", "bagpipe", "fiddle", "shanai",
        "tinklebell", "agogo", "steeldrums", "woodblock", "taiko", "melodictom", "synthdrum", "reversecymbal",
        "guitarfretnoise", "breathnoise", "seashore", "birdtweet", "telephone", "helicopter", "applause", "gunshot"
    ];
    private static readonly Dictionary<string, int> Programs = BuildPrograms();
    private static Dictionary<string, int> BuildPrograms()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < GeneralMidiNames.Length; i++)
        {
            map[GeneralMidiNames[i]] = i;
            map[GeneralMidiNames[i].Replace("_", "", StringComparison.Ordinal)] = i;
        }
        map["piano"] = 0; map["bass"] = 32; map["guitar"] = 24; map["trumpet"] = 56;
        map["cello"] = 42; map["organ"] = 19; map["synth"] = 80; map["flute"] = 73; map["violin"] = 40;
        return map;
    }
    public static int Resolve(string name)
    {
        if (int.TryParse(name, out var number) && number is >= 0 and <= 127) return number;
        if (Programs.TryGetValue(name, out var program)) return program;
        throw new InvalidOperationException($"Unknown instrument '{name}'. Use a General MIDI name or program 0-127.");
    }
    public static bool TryResolve(string name, out int program)
    {
        if (int.TryParse(name, out program)) return program is >= 0 and <= 127;
        return Programs.TryGetValue(name, out program);
    }
    public static bool TryGetName(int program, out string name)
    {
        if (program is >= 0 and < 128) { name = GeneralMidiNames[program]; return true; }
        name = string.Empty; return false;
    }
}
