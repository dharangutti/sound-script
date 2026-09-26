using System.Globalization;
using System.Text.Json;

namespace VideoLab;

public sealed class MediaDiagnostic(string code, string message, Exception? cause = null) : ArgumentException($"{code}: {message}", cause)
{ public string Code { get; } = code; }
public sealed record AssetFingerprint(string FullPath, long Bytes, string Sha256)
{
    public static AssetFingerprint Read(string path) => new(Path.GetFullPath(path), new FileInfo(path).Length, Ffmpeg.Hash(path));
}
public sealed record SourceInfo(string Path, string Kind, string Codec, decimal Duration, int Width, int Height,
    decimal SourceRate, int Channels, int SampleRate, decimal DisplayRotation);

public static class AssetProbe
{
    private static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) ? value.ToString() : "";
    private static decimal? Numeric(string value) => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    private static decimal? Ratio(string value)
    {
        var pair = value.Split('/');
        return pair.Length == 2 && Numeric(pair[0]) is decimal n && Numeric(pair[1]) is decimal d && d > 0 ? n / d : null;
    }
    public static async Task<SourceInfo> Validate(string path, string kind, int trim, int frames, int fps)
    {
        Composition.Require(kind is "video" or "audio" && trim >= 0 && frames > 0 && new[] { 24, 25, 30, 50, 60 }.Contains(fps), "Invalid probe span or stream kind.");
        path = System.IO.Path.GetFullPath(path);
        string span = $"'{path}', {kind}, normalized frames [{trim}, {(long)trim + frames}) at {fps} fps";
        if (kind == "audio") span += $", samples [{(long)trim * 48000 / fps}, {((long)trim + frames) * 48000 / fps}) at 48000 Hz";
        MediaDiagnostic Error(string code, string detail, Exception? cause = null) => new(code, $"{span}: {detail}", cause);
        if (!File.Exists(path)) throw Error("ASSET_NOT_FOUND", "asset not found.");
        string json;
        try { json = await Ffmpeg.Run("ffprobe", ["-v", "error", "-select_streams", kind == "video" ? "v:0" : "a:0", "-show_streams", "-show_format", "-of", "json", path]); }
        catch (InvalidOperationException e) { throw Error("INVALID_STREAM_METADATA", "container cannot be probed.", e); }
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
            throw Error(kind == "video" ? "VIDEO_STREAM_MISSING" : "AUDIO_STREAM_MISSING", $"required {kind} stream missing.");
        var stream = streams[0];
        var duration = Numeric(Text(stream, "duration")) ?? (root.TryGetProperty("format", out var format) ? Numeric(Text(format, "duration")) : null);
        if (duration is null) throw Error("DURATION_UNKNOWN", "no reliable stream/container duration.");
        if (duration <= 0) throw Error("INVALID_STREAM_METADATA", "duration must be positive.");
        decimal requested = ((decimal)trim + frames) / fps;
        if (duration * fps + 0.01m < (long)trim + frames)
            throw Error("ASSET_TOO_SHORT", $"requested end {Ffmpeg.Number(requested)}s exceeds detected duration {Ffmpeg.Number(duration.Value)}s.");
        int width = (int)(Numeric(Text(stream, "width")) ?? 0), height = (int)(Numeric(Text(stream, "height")) ?? 0);
        int channels = (int)(Numeric(Text(stream, "channels")) ?? 0), sampleRate = (int)(Numeric(Text(stream, "sample_rate")) ?? 0);
        decimal sourceRate = 0, rotation = 0;
        if (kind == "video")
        {
            if (width <= 0 || height <= 0) throw Error("INVALID_STREAM_METADATA", "invalid dimensions.");
            var sar = Text(stream, "sample_aspect_ratio");
            if (sar is not ("" or "N/A" or "1:1")) throw Error("UNSUPPORTED_GEOMETRY", "non-square sample aspect ratio is not validated.");
            if (Text(stream, "field_order") is not ("" or "unknown" or "progressive")) throw Error("UNSUPPORTED_INTERLACE", "interlaced video is outside v0.2 support.");
            var primaries = Text(stream, "color_primaries"); var transfer = Text(stream, "color_transfer"); var color = Text(stream, "color_space");
            if (primaries is not ("" or "unknown" or "bt709" or "bt470m" or "bt470bg" or "smpte170m" or "smpte240m") || color.Contains("2020", StringComparison.Ordinal) || transfer is "smpte2084" or "arib-std-b67" || Text(stream, "pix_fmt").Contains("10", StringComparison.Ordinal) || Text(stream, "pix_fmt").Contains("12", StringComparison.Ordinal))
                throw Error("UNSUPPORTED_COLOR", "HDR, wide-gamut and high-bit-depth inputs are outside the SDR validation boundary.");
            sourceRate = Ratio(Text(stream, "avg_frame_rate")) ?? 0;
            decimal nominal = Ratio(Text(stream, "r_frame_rate")) ?? 0, tick = Ratio(Text(stream, "time_base")) ?? 0;
            if (sourceRate <= 0 || sourceRate > 240 || nominal <= 0 || tick <= 0 || (sourceRate > 0 && tick > 1 / sourceRate + 0.000001m))
                throw Error("UNSUPPORTED_TIMING", "missing, invalid or coarse timing metadata.");
            if (Math.Abs(sourceRate - nominal) > 0.01m)
                throw Error("UNSUPPORTED_TIMING", "inconsistent nominal/average rate; VFR is outside the validated set.");
            if (stream.TryGetProperty("side_data_list", out var side))
                foreach (var entry in side.EnumerateArray()) rotation = Numeric(Text(entry, "rotation")) ?? rotation;

            // Validate the consumed prefix, not just a nominal fps label. Packet PTS are
            // sorted to account for B-frame decode order; timestamp gaps reveal common VFR.
            var packets = await Packets(path, "v:0", requested + 1);
            var stamps = packets.Where(p => p.Pts != null).Select(p => p.Pts!.Value).Order().ToArray();
            if (stamps.Length == 0 || stamps.Length != packets.Length)
                throw Error("UNSUPPORTED_TIMING", "video packet presentation timestamps are missing.");
            decimal ticksPerFrame = 1 / sourceRate / tick;
            decimal tolerance = Math.Abs(ticksPerFrame - Math.Round(ticksPerFrame)) < 0.000001m ? 0.000002m : Math.Max(0.000002m, tick * 1.1m);
            for (int i = 1; i < stamps.Length; i++)
                if (Math.Abs(stamps[i] - stamps[i - 1] - 1 / sourceRate) > tolerance)
                    throw Error("UNSUPPORTED_TIMING", "nonuniform presentation timestamps in consumed prefix (VFR or discontinuity).");
            decimal available = stamps[^1] - stamps[0] + 1 / sourceRate;
            if (available * fps + 0.01m < (long)trim + frames)
                throw Error("ASSET_TOO_SHORT", $"video packets end at {Ffmpeg.Number(available)}s; container duration is {Ffmpeg.Number(duration.Value)}s.");
        }
        else
        {
            if (channels is < 1 or > 2 || sampleRate is < 8000 or > 192000)
                throw Error("INVALID_STREAM_METADATA", "only mono/stereo audio at 8–192 kHz is validated.");
            if (Numeric(Text(stream, "duration")) == null)
            {
                var packets = await Packets(path, "a:0", requested + 0.1m);
                var valid = packets.Where(p => p.Pts != null && p.Duration > 0).ToArray();
                if (valid.Length == 0) throw Error("DURATION_UNKNOWN", "audio stream duration cannot be established from packets.");
                decimal available = valid.Max(p => p.Pts!.Value + p.Duration) - Math.Max(0, valid.Min(p => p.Pts!.Value));
                if (available * fps + 0.01m < (long)trim + frames) throw Error("ASSET_TOO_SHORT", $"audio packets end at {Ffmpeg.Number(available)}s.");
            }
        }
        return new(path, kind, Text(stream, "codec_name"), duration.Value, width, height, sourceRate, channels, sampleRate, rotation);
    }
    private sealed record Packet(decimal? Pts, decimal Duration);
    private static async Task<Packet[]> Packets(string path, string selector, decimal duration)
    {
        var json = await Ffmpeg.Run("ffprobe", ["-v", "error", "-select_streams", selector, "-read_intervals", $"%+{Ffmpeg.Number(duration)}", "-show_packets", "-show_entries", "packet=pts_time,duration_time", "-of", "json", path]);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("packets").EnumerateArray().Select(p => new Packet(Numeric(Text(p, "pts_time")), Numeric(Text(p, "duration_time")) ?? 0)).ToArray();
    }
}
