using System.Globalization;
using System.Text.Json;
using VideoLab;

try
{
    if (args.Length == 1 && args[0] == "selftest") { await Proof.Run(); return 0; }
    if (args.Length < 3 || !new[] { "inspect", "render" }.Contains(args[0]))
    {
        Console.Error.WriteLine("VideoLab: inspect <script.json> <frame> [name=value ...]\n          render <script.json> <output.mp4|webm> [name=value ...]\n          selftest (generates fixtures and reproducibility proof in artifacts/)");
        return 2;
    }
    var path = Path.GetFullPath(args[1]);
    var composition = Composition.Compile(File.ReadAllText(path), Path.GetDirectoryName(path)!);
    var runtime = composition.CreateRuntime();
    var changes = new Dictionary<string, decimal>();
    foreach (var arg in args.Skip(3))
    {
        var pair = arg.Split('=', 2);
        if (pair.Length != 2 || !decimal.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !changes.TryAdd(pair[0], value))
            throw new ArgumentException($"Invalid or duplicate parameter assignment: {arg}");
    }
    runtime.SetMany(changes); var snapshot = runtime.Bind();
    if (args[0] == "inspect") Console.WriteLine(JsonSerializer.Serialize(snapshot.SceneAt(int.Parse(args[2], CultureInfo.InvariantCulture)), new JsonSerializerOptions { WriteIndented = true }));
    else
    {
        Composition.Require(!string.Equals(path, Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase), "Output cannot replace the script.");
        await Ffmpeg.Render(snapshot, args[2]);
        Console.WriteLine($"{Path.GetFullPath(args[2])}\nSHA256 {Ffmpeg.Hash(args[2])}");
    }
    return 0;
}
catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or JsonException or System.ComponentModel.Win32Exception or FormatException or OverflowException)
{ Console.Error.WriteLine($"VideoLab: {e.Message}"); return 1; }
