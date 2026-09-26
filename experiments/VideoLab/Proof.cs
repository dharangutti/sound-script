using System.Text.Json;

namespace VideoLab;

internal static class Proof
{
    public static async Task Run()
    {
        if (!File.Exists("examples/demo.json")) throw new ArgumentException("Run selftest from experiments/VideoLab.");
        var assertions = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException($"FAIL: {name}"); assertions.Add(name); }
        void Reject(Action action, string name)
        {
            try { action(); } catch (ArgumentException) { assertions.Add(name); return; }
            throw new InvalidOperationException($"FAIL: accepted {name}");
        }
        Directory.CreateDirectory("artifacts/assets");
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i", "color=c=red:s=640x360:r=30:d=4", "-f", "lavfi", "-i", "sine=frequency=220:sample_rate=48000:duration=4", "-c:v", "libx264", "-threads", "1", "-pix_fmt", "yuv420p", "-c:a", "aac", "-fflags", "+bitexact", "artifacts/assets/first.mp4"]);
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i", "color=c=blue:s=480x360:r=25:d=4", "-c:v", "libx264", "-threads", "1", "-pix_fmt", "yuv420p", "artifacts/assets/second.mp4"]);
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=6", "-c:a", "pcm_s16le", "artifacts/assets/music.wav"]);
        var source = File.ReadAllText("examples/demo.json");
        var compiled = Composition.Compile(source, Path.GetFullPath("examples"));
        var runtime = compiled.CreateRuntime(); var a = runtime.Bind();
        runtime.SetMany(new Dictionary<string, decimal> { ["musicGain"] = 0.6m, ["accentX"] = 180 });
        var b = runtime.Bind();
        Check(ReferenceEquals(a.Composition, b.Composition) && a.Values["musicGain"] == 0.2m && b.Values["musicGain"] == 0.6m, "Shared structure, independent frozen snapshots");
        Reject(() => runtime.SetMany(new Dictionary<string, decimal> { ["musicGain"] = 0.1m, ["absent"] = 1 }), "Unknown parameter rejected atomically");
        Check(runtime.Bind().Values["musicGain"] == 0.6m, "Failed batch leaves runtime intact");
        Reject(() => runtime.SetMany(new Dictionary<string, decimal> { ["musicGain"] = -1 }), "Parameter bounds");
        Reject(() => Composition.Compile(source.Replace("\"fade\": 15", "\"fade\": 100"), "."), "Invalid transition");
        Reject(() => Composition.Compile(source.Replace("\"trim\": 15", "\"trim\": -1"), "."), "Negative trim");
        Reject(() => Composition.Compile(source.Replace("\"width\": 100", "\"width\": 99"), "."), "Odd shape dimension rejected");
        Reject(() => a.SceneAt(180), "Half-open timeline end");
        Check(compiled.Clips[1].At == 90 && a.SceneAt(0).Clips[0].SourceFrame == 15, "Sequence and source trim");
        Check(a.SceneAt(90).Clips[1].Opacity == 0 && a.SceneAt(98).Clips[1].Opacity == 8m / 15 && a.SceneAt(105).Clips.Length == 1, "Transition boundaries");
        Check(a.SceneAt(15).Shapes[0].X == 24 && b.SceneAt(15).Shapes[0].X == 180 && a.SceneAt(164).Shapes[0].X == 540 && a.SceneAt(165).Shapes.Length == 0, "Animated shape and snapshot parameter");
        var gap = Composition.Compile(source.Replace("\"fade\": 15", "\"at\": 120, \"frames\": 60, \"fade\": 0").Replace("\"frames\": 90,", ""), Path.GetFullPath("examples")).CreateRuntime().Bind();
        Check(gap.SceneAt(110).Clips.Length == 0, "Explicit placement permits black gaps");
        var tooShort = Composition.Compile(source.Replace("\"trim\": 15", "\"trim\": 1500"), Path.GetFullPath("examples")).CreateRuntime().Bind();
        File.WriteAllText("artifacts/preserved.mp4", "existing output");
        bool rejected = false;
        try { await Ffmpeg.Render(tooShort, "artifacts/preserved.mp4"); } catch (ArgumentException) { rejected = true; }
        Check(rejected && File.ReadAllText("artifacts/preserved.mp4") == "existing output", "Short input rejected without replacing existing output");
        var hashes = new SortedDictionary<string, string>();
        foreach (var ext in new[] { "mp4", "webm" })
        {
            foreach (var (name, snapshot) in new[] { ("A", a), ("B", b) })
            {
                Console.WriteLine($"Rendering snapshot {name} twice as {ext}...");
                var path = $"artifacts/{name}.{ext}"; var repeat = $"artifacts/{name}-repeat.{ext}";
                await Ffmpeg.Render(snapshot, path); await Ffmpeg.Render(snapshot, repeat);
                Check(Ffmpeg.Hash(path) == Ffmpeg.Hash(repeat), $"{name}.{ext}: byte-identical repeated export");
                hashes[$"{name}.{ext}"] = Ffmpeg.Hash(path);
                await Ffmpeg.Run("ffmpeg", ["-v", "error", "-xerror", "-i", path, "-f", "null", "-"]);
                var probe = await Ffmpeg.Run("ffprobe", ["-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", path]);
                using var doc = JsonDocument.Parse(probe); var stream = doc.RootElement.GetProperty("streams")[0];
                Check(stream.GetProperty("nb_read_frames").GetString() == "180" && stream.GetProperty("width").GetInt32() == 640 && stream.GetProperty("height").GetInt32() == 360, $"{name}.{ext}: 180 decodable frames at 640x360");
            }
            Check(hashes[$"A.{ext}"] != hashes[$"B.{ext}"], $"{ext}: parameters change rendered bytes");
        }
        async Task<byte[]> Frame(string input, int frame, string name)
        {
            var path = $"artifacts/{name}.rgb";
            await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", input, "-vf", $"select=eq(n\\,{frame})", "-frames:v", "1", "-pix_fmt", "rgb24", "-f", "rawvideo", path]);
            return File.ReadAllBytes(path);
        }
        var first = await Frame("artifacts/A.mp4", 0, "first");
        var middle = await Frame("artifacts/A.mp4", 98, "transition");
        var last = await Frame("artifacts/A.mp4", 179, "last");
        int pixel = (180 * 640 + 320) * 3;
        Check(first[pixel] > 200 && first[pixel + 2] < 20 && middle[pixel] > 70 && middle[pixel + 2] > 70 && last[pixel + 2] > 200 && last[pixel] < 20, "Decoded red → blended transition → blue pixels");
        var shapeA = await Frame("artifacts/A.mp4", 15, "shapeA"); var shapeB = await Frame("artifacts/B.mp4", 15, "shapeB");
        int accent = (318 * 640 + 40) * 3;
        Check(shapeA[accent + 1] > 150 && shapeB[accent + 1] < 30, "Decoded animated overlay uses bound position");
        var animated = await Frame("artifacts/A.mp4", 164, "animated");
        Check(animated[(318 * 640 + 580) * 3 + 1] > 150 && animated[accent + 1] < 30, "Decoded animation reaches destination");
        await Ffmpeg.Render(gap, "artifacts/gap.mp4");
        var gapPixels = await Frame("artifacts/gap.mp4", 110, "gap");
        Check(gapPixels[pixel] < 10 && gapPixels[pixel + 1] < 10 && gapPixels[pixel + 2] < 10, "Decoded explicit placement gap is black");
        await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", "artifacts/A.mp4", "-ss", "1", "-t", "1", "-vn", "-ac", "1", "-ar", "48000", "-f", "s16le", "artifacts/mix.pcm"]);
        var mix = File.ReadAllBytes("artifacts/mix.pcm");
        double Amplitude(int frequency)
        {
            double real = 0, imaginary = 0; int count = mix.Length / 2;
            for (int i = 0; i < count; i++)
            {
                double sample = BitConverter.ToInt16(mix, i * 2), angle = 2 * Math.PI * frequency * i / 48000;
                real += sample * Math.Cos(angle); imaginary += sample * Math.Sin(angle);
            }
            return 2 * Math.Sqrt(real * real + imaginary * imaginary) / count;
        }
        Check(Amplitude(220) > 100 && Amplitude(440) > 300 && Amplitude(1000) < 10, "Decoded mix contains both clip audio and music frequencies");
        async Task<double> Rms(string name)
        {
            var path = $"artifacts/{name}.pcm";
            await Ffmpeg.Run("ffmpeg", ["-v", "error", "-y", "-i", $"artifacts/{name}.mp4", "-ss", "4", "-t", "1", "-vn", "-ac", "1", "-ar", "48000", "-f", "s16le", path]);
            var bytes = File.ReadAllBytes(path); double sum = 0;
            for (int i = 0; i < bytes.Length; i += 2) { double value = BitConverter.ToInt16(bytes, i); sum += value * value; }
            return Math.Sqrt(sum / (bytes.Length / 2));
        }
        var ratio = await Rms("B") / await Rms("A");
        Check(ratio is > 2.8 and < 3.2, "Decoded audio gain scales by three");
        File.WriteAllText("artifacts/A.filter.txt", Ffmpeg.Plan(a, "A.mp4").FilterGraph);
        var inputHashes = Ffmpeg.Plan(a, "A.mp4").Inputs.Distinct().ToDictionary(p => Path.GetFileName(p)!, Ffmpeg.Hash);
        File.WriteAllText("artifacts/proof.json", JsonSerializer.Serialize(new { ffmpeg = (await Ffmpeg.Run("ffmpeg", ["-version"])).Split('\n')[0], assertions, hashes, inputHashes, gainRatio = ratio }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: {assertions.Count} checks. See artifacts/proof.json.");
    }
}
