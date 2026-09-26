using System.Text.Json;

namespace VideoLab;

internal static class NormalizationProof
{
    internal static async Task Run()
    {
        const string folder = "artifacts/normalization";
        Directory.CreateDirectory(folder);
        var checks = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException($"FAIL: {name}"); checks.Add(name); }
        async Task Reject(Func<Task> action, string code)
        {
            try { await action(); } catch (MediaDiagnostic e) when (e.Code == code) { checks.Add(code); return; }
            throw new InvalidOperationException($"FAIL: expected {code}");
        }
        Task Run(params string[] arguments) => Ffmpeg.Run("ffmpeg", new[] { "-v", "error", "-y" }.Concat(arguments));
        Snapshot Video(string asset, int trim = 0, int frames = 12, int width = 160, int height = 96)
            => RealMediaTests.Snapshot(new("test", Path.GetFullPath(asset), new(true, false), trim, frames, width, height), Path.GetFullPath("."));
        async Task<byte[]> Pixels(string input, string output)
        {
            await Run("-i", input, "-an", "-pix_fmt", "rgb24", "-f", "rawvideo", output); return File.ReadAllBytes(output);
        }
        var raw = new byte[80 * 48 * 3 * 60];
        for (int frame = 0; frame < 60; frame++) Array.Fill(raw, (byte)(16 + frame % 32 * 7), frame * 80 * 48 * 3, 80 * 48 * 3);
        File.WriteAllBytes(folder + "/markers.rgb", raw);
        foreach (var (rate, numeric, count) in new[] { ("24",24m,24), ("25",25m,25), ("30",30m,30), ("60",60m,60), ("24000/1001",24000m/1001,24), ("30000/1001",30000m/1001,30), ("60000/1001",60000m/1001,60) })
        {
            string name = rate.Replace('/', '-'), input = $"{folder}/rate-{name}.mp4", output = $"{folder}/mapped-{name}.mp4";
            await Run("-f", "rawvideo", "-pixel_format", "rgb24", "-video_size", "80x48", "-framerate", rate, "-i", folder + "/markers.rgb", "-frames:v", count.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:v", "libx264", "-threads", "1", "-crf", "0", "-pix_fmt", "yuv420p", input);
            var snapshot = Video(input, 3); await Ffmpeg.Render(snapshot, output);
            var rgb = await Pixels(output, $"{folder}/mapped-{name}.rgb");
            Check(rgb.Length == 12 * 160 * 96 * 3, $"{rate}: normalized output frame count");
            bool match = true;
            for (int f = 0; f < 12; f++)
            {
                int normalized = snapshot.SceneAt(f).Layers[0].SourceFrame!.Value;
                int physical = Enumerable.Range(0, count).Last(n => Math.Round(n * 30 / numeric, MidpointRounding.AwayFromZero) <= normalized);
                int expected = 16 + physical % 32 * 7, actual = rgb[(f * 160 * 96 + 48 * 160 + 80) * 3];
                match &= Math.Abs(expected - actual) <= 4;
            }
            Check(match, $"{rate}: SceneAt normalized source index matches observed frame markers");
        }
        foreach (var (name, w, h) in new[] { ("landscape1080",1920,1080), ("portrait1080",1080,1920), ("square",100,100), ("smaller",40,24), ("larger",640,384), ("unusual",300,50) })
        {
            string input = $"{folder}/{name}.mp4", output = $"{folder}/{name}-fit.mp4";
            await Run("-f", "lavfi", "-i", $"color=yellow:s={w}x{h}:r=30:d=0.5", "-c:v", "libx264", "-threads", "1", "-pix_fmt", "yuv420p", input);
            await Ffmpeg.Render(Video(input, frames: 3), output);
            var rgb = await Pixels(output, $"{folder}/{name}.rgb");
            int center = (48 * 160 + 80) * 3;
            bool bars = w * 96 != h * 160;
            Check(rgb[center] > 220 && rgb[center + 1] > 220 && (!bars || rgb[0] < 20), $"{name}: aspect fit, normalized plane and black bars");
        }
        string normal = folder + "/rate-30.mp4";
        await Ffmpeg.Render(Video(folder + "/landscape1080.mp4", frames: 3, width: 96, height: 160), folder + "/landscape-in-portrait.mp4");
        var portraitPixels = await Pixels(folder + "/landscape-in-portrait.mp4", folder + "/landscape-in-portrait.rgb");
        Check(portraitPixels[0] < 10 && portraitPixels[(80 * 96 + 48) * 3] > 220, "Landscape source fits as centered band inside portrait canvas");
        const string pivotSource = """
        {"width":160,"height":96,"fps":30,"frames":3,"parameters":{},"audio":[],"shapes":[],
        "videos":[{"asset":"../assets/crop.mp4","trim":0,"frames":3,
        "transform":{"width":40,"height":20,"rotation":90,"x":100,"y":20,"anchorX":0,"anchorY":0}}]}
        """;
        var pivot = Composition.Compile(pivotSource, Path.GetFullPath(folder)).CreateRuntime().Bind();
        await Ffmpeg.Render(pivot, folder + "/clockwise-pivot.mp4");
        var pivotPixels = await Pixels(folder + "/clockwise-pivot.mp4", folder + "/clockwise-pivot.rgb");
        Check(pivot.SceneAt(0).Layers[0].Transform.Raster().Left == 80 && pivotPixels[(25 * 160 + 90) * 3] > 200 && pivotPixels[(55 * 160 + 90) * 3 + 2] > 200, "Asymmetric pixels verify clockwise rotation about noncentral anchor");
        await AssetProbe.Validate(normal, "video", 0, 30, 30); checks.Add("Exact one-second request accepted");
        await AssetProbe.Validate(normal, "video", 0, 29, 30); checks.Add("Source one frame longer than requested accepted");
        await AssetProbe.Validate(normal, "video", 27, 3, 30); checks.Add("Trim at final included frames accepted");
        await AssetProbe.Validate(normal, "video", 3, 12, 30); checks.Add("Longer source and nonzero trim accepted");
        await Reject(() => AssetProbe.Validate(normal, "video", 28, 3, 30), "ASSET_TOO_SHORT");
        await Reject(() => AssetProbe.Validate(normal, "audio", 0, 3, 30), "AUDIO_STREAM_MISSING");
        await Reject(() => AssetProbe.Validate(folder + "/absent.mp4", "video", 0, 3, 30), "ASSET_NOT_FOUND");
        File.WriteAllText(folder + "/malformed.mp4", "not media");
        await Reject(() => AssetProbe.Validate(folder + "/malformed.mp4", "video", 0, 3, 30), "INVALID_STREAM_METADATA");
        await Run("-i", normal, "-c:v", "copy", "-f", "h264", folder + "/raw.h264");
        await Reject(() => AssetProbe.Validate(folder + "/raw.h264", "video", 0, 3, 30), "DURATION_UNKNOWN");
        await Run("-i", normal, "-vf", "select='not(eq(n,5))'", "-fps_mode", "vfr", "-c:v", "libx264", "-threads", "1", folder + "/vfr.mp4");
        await Reject(() => AssetProbe.Validate(folder + "/vfr.mp4", "video", 0, 15, 30), "UNSUPPORTED_TIMING");
        await Run("-i", normal, "-vf", "setsar=2", "-c:v", "libx264", "-threads", "1", folder + "/sar.mp4");
        await Reject(() => AssetProbe.Validate(folder + "/sar.mp4", "video", 0, 3, 30), "UNSUPPORTED_GEOMETRY");
        await Run("-i", normal, "-c:v", "copy", "-bsf:v", "h264_metadata=colour_primaries=9:transfer_characteristics=16:matrix_coefficients=9", folder + "/hdr.mp4");
        await Reject(() => AssetProbe.Validate(folder + "/hdr.mp4", "video", 0, 3, 30), "UNSUPPORTED_COLOR");
        await Run("-i", normal, "-c:v", "copy", "-bsf:v", "h264_metadata=colour_primaries=12:transfer_characteristics=1:matrix_coefficients=1", folder + "/p3.mp4");
        await Reject(() => AssetProbe.Validate(folder + "/p3.mp4", "video", 0, 3, 30), "UNSUPPORTED_COLOR");
        await Run("-i", normal, "-c:v", "libx264", "-threads", "1", "-flags", "+ilme+ildct", "-x264-params", "tff=1", folder + "/interlaced.mp4");
        await Reject(() => AssetProbe.Validate(folder + "/interlaced.mp4", "video", 0, 3, 30), "UNSUPPORTED_INTERLACE");
        await Run("-display_rotation:v:0", "90", "-i", normal, "-c", "copy", folder + "/rotation.mov");
        var rotatedInfo = await AssetProbe.Validate(folder + "/rotation.mov", "video", 0, 3, 30);
        Check(Math.Abs(rotatedInfo.DisplayRotation) == 90, "Rotation metadata fixture contains 90 degrees");
        await Ffmpeg.Render(Video(normal, frames: 3), folder + "/unrotated.mp4");
        await Ffmpeg.Render(Video(folder + "/rotation.mov", frames: 3), folder + "/ignored-rotation.mp4");
        var uprightPixels = await Pixels(folder + "/unrotated.mp4", folder + "/unrotated.rgb");
        var rotationPixels = await Pixels(folder + "/ignored-rotation.mp4", folder + "/rotation.rgb");
        Check(uprightPixels.SequenceEqual(rotationPixels), "Explicit noautorotate preserves encoded plane regardless of display matrix");
        await Run("-i", normal, "-c:v", "libvpx-vp9", "-threads", "1", "-crf", "20", "-b:v", "0", folder + "/vp9.webm");
        await Ffmpeg.Render(Video(folder + "/vp9.webm", frames: 3), folder + "/vp9-output.mp4"); checks.Add("CFR VP9 WebM input");
        await Run("-i", normal, "-c:v", "libx265", "-threads", "1", "-x265-params", "pools=none:frame-threads=1:log-level=error", "-tag:v", "hvc1", folder + "/hevc.mov");
        await Ffmpeg.Render(Video(folder + "/hevc.mov", frames: 3), folder + "/hevc-output.mp4"); checks.Add("SDR HEVC MOV input");
        await Run("-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=1", "-c:a", "pcm_s16le", folder + "/mono.wav");
        await Reject(() => AssetProbe.Validate(folder + "/mono.wav", "video", 0, 3, 30), "VIDEO_STREAM_MISSING");
        string audioJson = JsonSerializer.Serialize(new
        {
            width = 80, height = 48, fps = 30, frames = 15, parameters = new { }, videos = Array.Empty<object>(),
            shapes = new[] { new { at = 0, frames = 15, width = 80, height = 48, color = "000000" } },
            audio = new[] { new { asset = Path.GetFullPath(folder + "/mono.wav"), trim = 0, frames = 12, at = 3, gain = 1, when = "frame < 6" } }
        });
        var audioSnapshot = Composition.Compile(audioJson, Path.GetFullPath(".")).CreateRuntime().Bind();
        await Ffmpeg.Render(audioSnapshot, folder + "/mono-output.mp4");
        await Run("-i", folder + "/mono-output.mp4", "-vn", "-f", "s16le", folder + "/mono.pcm");
        var samples = File.ReadAllBytes(folder + "/mono.pcm");
        double Rms(int start, int channel) { double sum = 0; for (int i = start; i < start + 1600; i++) { double v = BitConverter.ToInt16(samples, i * 4 + channel * 2); sum += v * v; } return Math.Sqrt(sum / 1600); }
        Check(Math.Abs(Rms(8000, 0) - Rms(8000, 1)) < 1 && Rms(8000, 0) > 100, "44.1 kHz mono becomes equal 48 kHz stereo channels");
        Check(Rms(800, 0) < 3 && Rms(20000, 0) < 3 && !audioSnapshot.SceneAt(10).Audio[0].Included, "Decoded leading silence and conditional audio silence agree with SceneAt");
        var repeated = RealMediaTests.Snapshot(new("repeat", Path.GetFullPath(normal), new(true, false), Frames: 3, Repeat: true), Path.GetFullPath("."));
        await Ffmpeg.Render(repeated, folder + "/repeated-source.mp4"); await RealMediaTests.VerifyOutput(folder + "/repeated-source.mp4", repeated.Composition.Script); checks.Add("Repeated asset references preserve timeline duration");
        string preserved = folder + "/preserved.mp4"; File.WriteAllText(preserved, "keep");
        await Reject(() => Ffmpeg.Render(Video(folder + "/vfr.mp4"), preserved), "UNSUPPORTED_TIMING");
        Check(File.ReadAllText(preserved) == "keep" && !Directory.EnumerateFiles(folder, ".videolab-*").Any(), "Rejected media preserves output and leaves no temporary artifacts");
        Check(await RealMediaTests.Run(folder + "/no-manifest.json") == 0, "Optional realtest skips missing manifest successfully");
        File.WriteAllText(folder + "/missing-asset-manifest.json", "{\"cases\":[{\"name\":\"missing\",\"asset\":\"missing.mp4\",\"expected\":{\"hasVideo\":true,\"hasAudio\":false}}]}");
        Check(await RealMediaTests.Run(folder + "/missing-asset-manifest.json") == 1, "Realtest validation failure returns 1");
        File.WriteAllText(folder + "/proof.json", JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: {checks.Count} v0.2 normalization checks.");
    }
}
