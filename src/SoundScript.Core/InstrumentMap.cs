namespace SoundScript.Core;

public static class InstrumentMap
{
    public const int DefaultProgram = 0;

    private static readonly Dictionary<string, int> Programs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piano"] = 0,
        ["violin"] = 40,
        ["flute"] = 73,
        ["bass"] = 32,
        ["guitar"] = 24,
        ["trumpet"] = 56,
        ["cello"] = 42,
        ["organ"] = 19,
        ["synth"] = 80
    };

    public static int Resolve(string name)
    {
        if (Programs.TryGetValue(name, out var program))
            return program;

        throw new InvalidOperationException($"Unknown instrument '{name}'.");
    }

    public static bool TryResolve(string name, out int program) =>
        Programs.TryGetValue(name, out program);

    public static bool TryGetName(int program, out string name)
    {
        foreach (var pair in Programs)
        {
            if (pair.Value == program)
            {
                name = pair.Key;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }
}
