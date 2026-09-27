using System.Globalization;
using System.Text.Json;
using VideoLab;

try
{
    if (args.Length == 1 && args[0] == "annotationtest") { await AnnotationProof.Run(); return 0; }
    if (args.Length == 1 && args[0] == "audiencetest") { await AudienceProof.Run(); return 0; }
    if (args.Length == 1 && args[0] == "editingtest") { await EditingProof.Run(); return 0; }
    if (args.Length is 1 or 2 && args[0] == "serve")
    {
        await LocalWorkbench.Run(args.Length == 2 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 8745); return 0;
    }
    if (args.Length == 1 && args[0] == "webproof")
    {
        await Proof.Run(); await ProgrammableProof.Run(); await NormalizationProof.Run(); await EditingProof.Run(); await AudienceProof.Run(); await AnnotationProof.Run();
        await WebProof.Publish(); return 0;
    }
    if (args.Length > 0 && args[0] == "realtest")
    {
        if (args.Length != 2) { Console.Error.WriteLine("VideoLab: realtest <optional-manifest.json>"); return 2; }
        return await RealMediaTests.Run(args[1]);
    }
    if (args.Length == 1 && args[0] == "selftest") { await Proof.Run(); await ProgrammableProof.Run(); await NormalizationProof.Run(); await EditingProof.Run(); await AudienceProof.Run(); await AnnotationProof.Run(); return 0; }
    if (args.Length < 3 || !new[] { "inspect", "render", "plan", "batch" }.Contains(args[0]))
    {
        Console.Error.WriteLine("VideoLab: inspect <script.json> <frame> [name=value ...]\n          render <script.json> <output.mp4|webm> [name=value ...]\n          plan <script.json> <output.mp4|webm> [name=value ...]\n          batch <script.json> <batch.json>\n          serve [port] (private local browser workbench)\n          webproof (rebuild static gallery)\n          realtest <optional-manifest.json>\n          selftest (generates fixtures and reproducibility proof in artifacts/)");
        return 2;
    }
    var path = Path.GetFullPath(args[1]);
    var source = File.ReadAllText(path);
    string? annotationPath = null;
    var assignments = args.Skip(3).ToList();
    var annotationFlag = assignments.IndexOf("--annotations");
    if (annotationFlag >= 0)
    {
        Composition.Require(args[0] != "batch" && annotationFlag + 1 < assignments.Count, "Use per-record annotations in batches; --annotations requires a file.");
        annotationPath = Path.GetFullPath(assignments[annotationFlag + 1]);
        source = Annotations.Expand(source, Annotations.ReadFile(annotationPath), Path.GetDirectoryName(path)!);
        assignments.RemoveRange(annotationFlag, 2);
    }
    var composition = Composition.Compile(source, Path.GetDirectoryName(path)!);
    if (args[0] == "batch")
    {
        Composition.Require(args.Length == 3, "batch takes exactly a script and batch file.");
        var batchPath = Path.GetFullPath(args[2]);
        await Batches.Render(composition, File.ReadAllText(batchPath), Path.GetDirectoryName(batchPath)!, [path, batchPath]);
        return 0;
    }
    var runtime = composition.CreateRuntime();
    var changes = new Dictionary<string, JsonElement>();
    foreach (var arg in assignments)
    {
        var pair = arg.Split('=', 2);
        if (pair.Length != 2) throw new ArgumentException($"Invalid assignment: {arg}");
        var value = composition.Script.TypedParameters?.GetValueOrDefault(pair[0])?.Type switch
        {
            "enum" => JsonSerializer.SerializeToElement(pair[1]),
            "boolean" => bool.TryParse(pair[1], out var boolean) ? JsonSerializer.SerializeToElement(boolean) : throw new ArgumentException("Boolean assignment requires true or false."),
            _ => decimal.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? JsonSerializer.SerializeToElement(number) : throw new ArgumentException("Numeric assignment required.")
        };
        if (!changes.TryAdd(pair[0], value))
            throw new ArgumentException($"Invalid or duplicate parameter assignment: {arg}");
    }
    runtime.SetBindings(changes); var snapshot = runtime.Bind();
    if (args[0] == "inspect") Console.WriteLine(JsonSerializer.Serialize(snapshot.SceneAt(int.Parse(args[2], CultureInfo.InvariantCulture)), new JsonSerializerOptions { WriteIndented = true }));
    else if (args[0] == "plan") Console.WriteLine(JsonSerializer.Serialize(Ffmpeg.Plan(snapshot, args[2]), new JsonSerializerOptions { WriteIndented = true }));
    else
    {
        Composition.Require(!string.Equals(path, Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase), "Output cannot replace the script.");
        Composition.Require(annotationPath == null || !string.Equals(annotationPath, Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase), "Output cannot replace annotation input.");
        await Ffmpeg.Render(snapshot, args[2]);
        Console.WriteLine($"{Path.GetFullPath(args[2])}\nSHA256 {Ffmpeg.Hash(args[2])}");
    }
    return 0;
}
catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or JsonException or System.ComponentModel.Win32Exception or FormatException or OverflowException)
{ Console.Error.WriteLine($"VideoLab: {e.Message}"); return 1; }
