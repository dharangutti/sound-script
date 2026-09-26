using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace VideoLab;

public sealed record OutputSettings(int Width, int Height, int Fps, int Frames, int SampleRate, string Container);
public sealed record RenderPlan(string FilterGraph, string[] Arguments, string[] Inputs, OutputSettings Expected);

public static class Ffmpeg
{
    internal static string Number(decimal value) => value.ToString("0.#########", CultureInfo.InvariantCulture);
    public static RenderPlan Plan(Snapshot snapshot, string output)
    {
        if (snapshot.Composition.Programmable) return ProgrammableBackend.Plan(snapshot, output);
        var c = snapshot.Composition; var s = c.Script;
        string T(int frames) => Number((decimal)frames / s.Fps);
        string Asset(string path) => Path.GetFullPath(path, c.AssetDirectory);
        var inputs = c.Clips.Select(v => Asset(v.Asset)).Concat(s.Audio.Select(a => Asset(a.Asset))).ToArray();
        var graph = new List<string> { $"color=c=black:s={s.Width}x{s.Height}:r={s.Fps}:d={T(s.Frames)},format=yuv420p[base]" };
        string current = "base";
        for (int i = 0; i < c.Clips.Length; i++)
        {
            var v = c.Clips[i];
            var fade = v.Fade == 0 ? "" : $",fade=t=in:st=0:d={T(v.Fade)}:alpha=1";
            graph.Add($"[{i}:v:0]setpts=PTS-STARTPTS,fps={s.Fps},trim=start_frame={v.Trim}:end_frame={(long)v.Trim + v.Frames},setpts=PTS-STARTPTS,scale={s.Width}:{s.Height}:force_original_aspect_ratio=decrease,pad={s.Width}:{s.Height}:(ow-iw)/2:(oh-ih)/2,setsar=1,format=rgba{fade},setpts=PTS+{T(v.At)}/TB[v{i}]");
            graph.Add($"[{current}][v{i}]overlay=eof_action=pass:repeatlast=0:enable='gte(t,{T(v.At)})*lt(t,{T(v.At + v.Frames)})'[layer{i}]");
            current = $"layer{i}";
        }
        for (int i = 0; i < s.Shapes.Length; i++)
        {
            var o = s.Shapes[i]; var x = snapshot.Values[o.X!];
            graph.Add($"color=c=0x{o.Color}:s={o.Width}x{o.Height}:r={s.Fps}:d={T(s.Frames)},format=rgba[shape{i}]");
            graph.Add($"[{current}][shape{i}]overlay=x='{Number(x)}+({Number(o.ToX!.Value - x)})*clip((t-{T(o.At)})/{T(Math.Max(1, o.Frames - 1))},0,1)':y={o.Y}:eval=frame:eof_action=pass:enable='gte(t,{T(o.At)})*lt(t,{T(o.At + o.Frames)})'[shapeLayer{i}]");
            current = $"shapeLayer{i}";
        }
        graph.Add($"[{current}]trim=end_frame={s.Frames},setpts=PTS-STARTPTS,format=yuv420p[vout]");
        graph.Add($"anullsrc=r=48000:cl=stereo,atrim=end_sample={(long)s.Frames * 48000 / s.Fps}[silence]");
        var audioLabels = "[silence]";
        for (int i = 0; i < s.Audio.Length; i++)
        {
            var a = s.Audio[i];
            graph.Add($"[{c.Clips.Length + i}:a:0]asetpts=PTS-STARTPTS,aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,atrim=start_sample={(long)a.Trim * 48000 / s.Fps}:end_sample={((long)a.Trim + a.Frames) * 48000 / s.Fps},asetpts=PTS-STARTPTS,volume={Number(snapshot.Values[a.Gain.GetString()!])},adelay={((long)a.At * 48000 / s.Fps)}S:all=1[a{i}]");
            audioLabels += $"[a{i}]";
        }
        graph.Add($"{audioLabels}amix=inputs={s.Audio.Length + 1}:duration=first:normalize=0:dropout_transition=0,alimiter=limit=0.95:level=0:latency=1,atrim=end_sample={(long)s.Frames * 48000 / s.Fps}[aout]");
        return Assemble(s, output, inputs, graph);
    }
    internal static RenderPlan Assemble(Script s, string output, string[] inputs, List<string> graph)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-xerror", "-nostdin", "-y", "-filter_complex_threads", "1", "-fflags", "+bitexact" };
        foreach (var input in inputs) args.AddRange(["-threads", "1", "-i", input]);
        args.AddRange(["-filter_complex", string.Join(";", graph), "-map", "[vout]", "-map", "[aout]", "-map_metadata", "-1", "-map_chapters", "-1", "-threads", "1", "-flags:v", "+bitexact", "-flags:a", "+bitexact"]);
        switch (Path.GetExtension(output).ToLowerInvariant())
        {
            case ".mp4": args.AddRange(["-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart"]); break;
            case ".webm": args.AddRange(["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-row-mt", "0", "-c:a", "libopus", "-b:a", "128k", "-fflags", "+bitexact"]); break;
            default: throw new ArgumentException("Output must be .mp4 or .webm.");
        }
        args.AddRange(["-t", Number((decimal)s.Frames / s.Fps), output]);
        return new(string.Join(";\n", graph), args.ToArray(), inputs, new(s.Width, s.Height, s.Fps, s.Frames, 48000, Path.GetExtension(output).ToLowerInvariant()));
    }

    public static async Task<string> Run(string executable, IEnumerable<string> args)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Cannot start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var text = await stdout; var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} exited {process.ExitCode}: {error}");
        return text;
    }

    public static async Task Render(Snapshot snapshot, string output)
    {
        output = Path.GetFullPath(output);
        var plan = Plan(snapshot, output);
        Composition.Require(!plan.Inputs.Contains(output, StringComparer.OrdinalIgnoreCase), "Output cannot replace an input asset.");
        // Probe every selected stream before starting an expensive export. No implicit source looping.
        for (int i = 0; i < plan.Inputs.Length; i++)
        {
            Composition.Require(File.Exists(plan.Inputs[i]), $"Missing asset: {plan.Inputs[i]}");
            bool video = i < snapshot.Composition.Clips.Length;
            var info = await Run("ffprobe", ["-v", "error", "-select_streams", video ? "v:0" : "a:0", "-show_entries", "stream=duration:format=duration", "-of", "json", plan.Inputs[i]]);
            using var doc = JsonDocument.Parse(info);
            var streams = doc.RootElement.GetProperty("streams");
            Composition.Require(streams.GetArrayLength() > 0, $"Required {(video ? "video" : "audio")} stream missing: {plan.Inputs[i]}");
            var stream = streams[0];
            string? duration = stream.TryGetProperty("duration", out var d) ? d.GetString() : null;
            if (duration == null || duration == "N/A") duration = doc.RootElement.GetProperty("format").TryGetProperty("duration", out d) ? d.GetString() : null;
            var needed = video ? (long)snapshot.Composition.Clips[i].Trim + snapshot.Composition.Clips[i].Frames : (long)snapshot.Composition.Script.Audio[i - snapshot.Composition.Clips.Length].Trim + snapshot.Composition.Script.Audio[i - snapshot.Composition.Clips.Length].Frames;
            Composition.Require(decimal.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds * snapshot.Composition.Script.Fps + 0.01m >= needed, $"Asset is too short or duration is unknown: {plan.Inputs[i]}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = Path.Combine(Path.GetDirectoryName(output)!, $".videolab-{Guid.NewGuid():N}{Path.GetExtension(output)}");
        var graphFile = temporary + ".filters";
        try
        {
            // Long bounded frame plans exceed Windows' command-line limit. The public plan
            // still contains the complete graph; only transport is moved to a temporary file.
            var render = Plan(snapshot, temporary);
            var arguments = render.Arguments.ToArray();
            int index = Array.IndexOf(arguments, "-filter_complex");
            await File.WriteAllTextAsync(graphFile, render.FilterGraph);
            arguments[index] = "-/filter_complex"; arguments[index + 1] = graphFile;
            await Run("ffmpeg", arguments);
            File.Move(temporary, output, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(graphFile)) File.Delete(graphFile); }
    }
    public static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
