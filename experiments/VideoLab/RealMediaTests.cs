using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoLab;

public sealed record MediaExpectation(bool HasVideo, bool HasAudio, string? Rejection = null);
public sealed record MediaCase(string Name, string Asset, MediaExpectation Expected, int Trim = 0,
    int Frames = 15, int Width = 160, int Height = 96, bool Repeat = false);
public sealed record MediaManifest(ImmutableArray<MediaCase> Cases);

internal static class RealMediaTests
{
    internal static async Task<int> Run(string manifestPath)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        if (!File.Exists(manifestPath)) { Console.WriteLine("SKIP: optional real-media manifest not found."); return 0; }
        string directory = Path.GetDirectoryName(manifestPath)!;
        var source = File.ReadAllText(manifestPath);
        Composition.Require(source.Length <= 100000, "Real-media manifest exceeds 100,000 characters.");
        using var doc = JsonDocument.Parse(source); JsonRules.NoDuplicates(doc.RootElement);
        var manifest = JsonSerializer.Deserialize<MediaManifest>(source, new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true })!;
        Composition.Require(manifest != null && !manifest.Cases.IsDefault && manifest.Cases.Length is > 0 and <= 32, "Manifest requires 1–32 cases.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Cases)
            Composition.Require(item != null && item.Expected != null && !string.IsNullOrWhiteSpace(item.Asset) && System.Text.RegularExpressions.Regex.IsMatch(item.Name, "^[a-zA-Z0-9_-]{1,64}$") && names.Add(item.Name), "Invalid or duplicate real-media case.");
        var protectedPaths = manifest.Cases.Select(c => Path.GetFullPath(c.Asset, directory)).Append(manifestPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string report = Path.Combine(directory, "results", "report.json");
        Composition.Require(!protectedPaths.Contains(report), "Report cannot replace an asset or manifest.");
        // Validate every output destination and composition before writing any output.
        var cases = manifest.Cases.Select(c => (Case: c, Snapshot: Snapshot(c, directory))).ToArray();
        foreach (var (item, _) in cases)
            foreach (var extension in new[] { "mp4", "webm" })
                foreach (var suffix in new[] { "", "-repeat" })
                    Composition.Require(!protectedPaths.Contains(Path.Combine(directory, "results", item.Name + suffix + "." + extension)), "Test output cannot replace source assets.");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var results = new List<object>(); int failures = 0;
        foreach (var (item, snapshot) in cases)
        {
            string asset = Path.GetFullPath(item.Asset, directory);
            AssetFingerprint? before = File.Exists(asset) ? AssetFingerprint.Read(asset) : null;
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var metadata = new List<SourceInfo>(); string outcome = "pass"; string? error = null;
            try
            {
                if (item.Expected.HasVideo) metadata.Add(await AssetProbe.Validate(asset, "video", item.Trim, item.Frames, 30));
                if (item.Expected.HasAudio) metadata.Add(await AssetProbe.Validate(asset, "audio", item.Trim, item.Frames, 30));
                var streams = await Ffmpeg.Run("ffprobe", ["-v", "error", "-show_entries", "stream=codec_type", "-of", "json", asset]);
                using var sd = JsonDocument.Parse(streams);
                var types = sd.RootElement.GetProperty("streams").EnumerateArray().Select(s => s.GetProperty("codec_type").GetString()).ToArray();
                Composition.Require(types.Contains("video") == item.Expected.HasVideo && types.Contains("audio") == item.Expected.HasAudio, "Stream presence differs from manifest expectation.");
                Composition.Require(item.Expected.Rejection == null, "Expected rejection did not occur.");
                foreach (var extension in new[] { "mp4", "webm" })
                {
                    string output = Path.Combine(directory, "results", item.Name + "." + extension), repeat = Path.Combine(directory, "results", item.Name + "-repeat." + extension);
                    await Ffmpeg.Render(snapshot, output); await Ffmpeg.Render(snapshot, repeat);
                    Composition.Require(Ffmpeg.Hash(output) == Ffmpeg.Hash(repeat), "Repeated output hash differs.");
                    await VerifyOutput(output, snapshot.Composition.Script);
                    hashes[extension] = Ffmpeg.Hash(output);
                }
            }
            catch (MediaDiagnostic e) when (e.Code == item.Expected.Rejection) { outcome = "expected-rejection"; error = e.Code; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or JsonException)
            { outcome = "failure"; error = e.Message; }
            if (before != null && (!File.Exists(asset) || AssetFingerprint.Read(asset) != before))
            { outcome = "failure"; error = "Source changed during validation."; }
            if (outcome == "failure") failures++;
            results.Add(new { item.Name, outcome, error, metadata, fingerprint = before, outputHashes = hashes });
            Console.WriteLine($"{item.Name}: {outcome}{(error == null ? "" : " — " + error)}");
        }
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { results, failures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Real-media validation: {cases.Length - failures}/{cases.Length} passed. Private report: {report}");
        return failures == 0 ? 0 : 1;
    }
    internal static Snapshot Snapshot(MediaCase item, string directory)
    {
        int count = item.Repeat ? 2 : 1, duration = checked(item.Frames * count);
        var json = JsonSerializer.Serialize(new
        {
            width = item.Width, height = item.Height, fps = 30, frames = duration,
            parameters = new Dictionary<string, object> { ["gain"] = new { @default = 0.3m, min = 0, max = 1 } },
            videos = item.Expected.HasVideo ? Enumerable.Range(0, count).Select(i => new { asset = item.Asset, trim = item.Trim, frames = item.Frames, at = i * item.Frames, transform = new { opacity = 1 } }).ToArray() : [],
            audio = item.Expected.HasAudio ? Enumerable.Range(0, count).Select(i => new { asset = item.Asset, trim = item.Trim, frames = item.Frames, at = i * item.Frames, gain = "gain" }).ToArray() : [],
            shapes = item.Expected.HasVideo ? [] : new[] { new { at = 0, frames = duration, width = item.Width, height = item.Height, color = "000000" } }
        });
        return Composition.Compile(json, directory).CreateRuntime().Bind();
    }
    internal static async Task VerifyOutput(string path, Script script)
    {
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-xerror", "-i", path, "-f", "null", "-"]);
        var json = await Ffmpeg.Run("ffprobe", ["-v", "error", "-count_frames", "-show_entries", "stream=codec_type,width,height,nb_read_frames,sample_rate,channels", "-of", "json", path]);
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.Single(s => s.GetProperty("codec_type").GetString() == "video");
        var audio = streams.Single(s => s.GetProperty("codec_type").GetString() == "audio");
        Composition.Require(video.GetProperty("width").GetInt32() == script.Width && video.GetProperty("height").GetInt32() == script.Height && video.GetProperty("nb_read_frames").GetString() == script.Frames.ToString(System.Globalization.CultureInfo.InvariantCulture), "Decoded output video settings mismatch.");
        Composition.Require(audio.GetProperty("sample_rate").GetString() == "48000" && audio.GetProperty("channels").GetInt32() == 2, "Decoded output audio settings mismatch.");
    }
}
