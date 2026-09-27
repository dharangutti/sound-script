using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoLab;

// Optional local-only adapter. No production references, cloud upload or arbitrary
// source/path endpoint: callers choose an approved example and bounded bindings.
internal sealed class LocalWorkbench
{
    private const long MaxFile = 50 * 1024 * 1024;
    private static readonly string[] Demos = ["showcase", "demo", "transforms", "expressions", "effects", "conditional", "data-sequence"];
    private readonly string lab = Path.GetFullPath(".");
    private readonly string root = Path.Combine(Path.GetTempPath(), "videolab-session-" + Guid.NewGuid().ToString("N"));
    private readonly string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly SemaphoreSlim gate = new(1);
    private readonly Dictionary<string, (string File, string Name)> uploads = new();
    private readonly Dictionary<string, (Composition Composition, Runtime Runtime, string Source)> cache = new();
    private CancellationTokenSource? job;
    private string origin = "";
    private DateTime activity = DateTime.UtcNow;
    private Action stop = () => { };

    public static async Task Run(int port)
    {
        Composition.Require(port is >= 1024 and <= 65535, "Port must be 1024–65535.");
        var app = new LocalWorkbench();
        Composition.Require(File.Exists(Path.Combine(app.lab, "web/site/index.html")), "Run serve from experiments/VideoLab with its built web/site distribution.");
        app.origin = $"http://127.0.0.1:{port}";
        app.Prepare();
        using var listener = new HttpListener(); listener.Prefixes.Add(app.origin + "/"); listener.Start();
        using var stopped = new CancellationTokenSource();
        app.stop = () => { stopped.Cancel(); app.job?.Cancel(); listener.Stop(); };
        ConsoleCancelEventHandler stop = (_, e) => { e.Cancel = true; stopped.Cancel(); app.job?.Cancel(); listener.Stop(); };
        Console.CancelKeyPress += stop;
        Console.WriteLine($"VideoLab local workbench: {app.origin}/labs/videolab/\nFiles stay in a temporary local session. Ctrl+C stops the workbench and removes session files.");
        var tasks = new List<Task>();
        using var expiry = new Timer(_ =>
        {
            if (DateTime.UtcNow - app.activity <= TimeSpan.FromMinutes(30) || !app.gate.Wait(0)) return;
            try { app.Clean(); app.Prepare(); app.activity = DateTime.UtcNow; }
            catch (IOException) { Console.Error.WriteLine("Session cleanup was deferred; close active media downloads and reset the session."); }
            finally { app.gate.Release(); }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                tasks.RemoveAll(t => t.IsCompleted);
                tasks.Add(app.Handle(context));
            }
        }
        catch (Exception e) when (e is HttpListenerException or ObjectDisposedException && stopped.IsCancellationRequested) { }
        finally
        {
            await expiry.DisposeAsync();
            app.job?.Cancel();
            try { await Task.WhenAll(tasks); }
            finally { Console.CancelKeyPress -= stop; app.Clean(); }
        }
    }

    private void Prepare()
    {
        Directory.CreateDirectory(Path.Combine(root, "uploads"));
        Directory.CreateDirectory(Path.Combine(root, "results"));
        Directory.CreateDirectory(Path.Combine(root, "samples"));
        foreach (var file in Directory.GetFiles(Path.Combine(lab, "web/samples")))
            if (Path.GetExtension(file) is ".mp4" or ".wav") File.Copy(file, Path.Combine(root, "samples", Path.GetFileName(file)), true);
    }
    private void Clean()
    {
        var full = Path.GetFullPath(root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("videolab-session-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe session cleanup target.");
        if (Directory.Exists(full)) Directory.Delete(full, true);
        uploads.Clear(); cache.Clear();
    }
    private static async Task Json(HttpListenerResponse response, object value, int status = 200)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value); response.StatusCode = status;
        response.ContentType = "application/json"; response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }
    private async Task Handle(HttpListenerContext context)
    {
        var request = context.Request; var response = context.Response;
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        response.Headers["Cache-Control"] = "no-store";
        bool locked = false;
        try
        {
            if (request.Url?.GetLeftPart(UriPartial.Authority) != origin) { await Json(response, new { error = "Loopback host required." }, 403); return; }
            var route = request.Url.AbsolutePath;
            if (route.StartsWith("/api/", StringComparison.Ordinal))
            {
                if (request.Headers["Origin"] is string sentOrigin && sentOrigin != origin)
                { await Json(response, new { error = "Cross-origin access denied." }, 403); return; }
                if (route == "/api/session" && request.HttpMethod == "GET")
                {
                    if (request.Headers["Sec-Fetch-Site"] is string site && site != "same-origin")
                    { await Json(response, new { error = "Open the workbench page first." }, 403); return; }
                    await Json(response, new { token, maxFileBytes = MaxFile, local = true }); return;
                }
                if (request.HttpMethod != "POST" || request.Headers["X-VideoLab-Session"] != token || request.Headers["Origin"] != origin)
                { await Json(response, new { error = "Invalid local session." }, 403); return; }
                if (route == "/api/cancel") { job?.Cancel(); await Json(response, new { cancelled = true }); return; }
                if (route == "/api/stop") { await Json(response, new { stopped = true }); stop(); return; }
                locked = await gate.WaitAsync(0);
                if (!locked) { await Json(response, new { error = "A local operation is already running. Wait or cancel the render." }, 409); return; }
                if (DateTime.UtcNow - activity > TimeSpan.FromMinutes(30)) { Clean(); Prepare(); }
                activity = DateTime.UtcNow;
                if (route == "/api/reset") { Clean(); Prepare(); await Json(response, new { reset = true }); return; }
                if (route == "/api/upload") { await Upload(request, response); return; }
                Composition.Require(request.ContentLength64 is > 0 and <= 16384, "Request must be a small JSON document.");
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = JsonNode.Parse(await reader.ReadToEndAsync())?.AsObject() ?? throw new ArgumentException("JSON object required.");
                var snapshot = Bind(body, out var source);
                if (route == "/api/bind")
                {
                    await Json(response, new { parameters = snapshot.Values, scenes = Enumerable.Range(0, snapshot.Composition.Script.Frames).Select(snapshot.SceneAt), tracks = WebProof.Tracks(snapshot.Composition), script = JsonNode.Parse(source) }); return;
                }
                if (route == "/api/render")
                {
                    var format = body["format"]?.GetValue<string>(); Composition.Require(format is "mp4" or "webm", "Choose MP4 or WebM.");
                    var name = Guid.NewGuid().ToString("N") + "." + format;
                    var output = Path.Combine(root, "results", name);
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3)); job = cancellation;
                    try
                    {
                        await Ffmpeg.Render(snapshot, output, cancellation.Token);
                        // Bound session output storage without touching the current result.
                        foreach (var old in Directory.GetFiles(Path.Combine(root, "results")).Where(p => p != output)) File.Delete(old);
                        await Json(response, new { url = "/results/" + name, sha256 = Ffmpeg.Hash(output) });
                    }
                    finally { job = null; }
                    return;
                }
                await Json(response, new { error = "Unknown operation." }, 404); return;
            }
            if (request.HttpMethod != "GET") { response.StatusCode = 405; return; }
            string file;
            if (route.StartsWith("/results/", StringComparison.Ordinal))
            {
                var name = route[9..];
                Composition.Require(System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{32}\\.(mp4|webm)$"), "Invalid result.");
                file = Path.Combine(root, "results", name);
            }
            else
            {
                if (route is "/" or "/labs/" or "/labs/videolab") { response.Redirect("/labs/videolab/"); return; }
                Composition.Require(route.StartsWith("/labs/videolab/", StringComparison.Ordinal), "Unknown route.");
                var relative = Uri.UnescapeDataString(route[15..]); if (relative == "") relative = "index.html";
                Composition.Require(!relative.Contains('\\') && !relative.Split('/').Any(p => p is "." or ".."), "Invalid static path.");
                var site = Path.GetFullPath(Path.Combine(lab, "web/site")) + Path.DirectorySeparatorChar;
                file = Path.GetFullPath(Path.Combine(site, relative)); Composition.Require(file.StartsWith(site, StringComparison.OrdinalIgnoreCase), "Invalid static path.");
            }
            if (route == "/labs/videolab/publication.json") { await Json(response, new { mode = "private local workbench", publishedArtifact = false }); return; }
            if (!File.Exists(file)) { response.StatusCode = 404; return; }
            if (Path.GetFileName(file) == "index.html")
            {
                var html = (await File.ReadAllTextAsync(file)).Replace("<body>", "<body data-local-workbench=\"true\">", StringComparison.Ordinal);
                var bytes = Encoding.UTF8.GetBytes(html); response.ContentType = "text/html"; response.ContentLength64 = bytes.Length;
                await response.OutputStream.WriteAsync(bytes); return;
            }
            await ServeFile(request, response, file);
        }
        catch (MediaDiagnostic e) { await Json(response, new { error = $"{e.Code}: Media is malformed, too short, or outside the supported SDR/CFR stream policy. Choose another file." }, 400); }
        catch (OperationCanceledException) { await Json(response, new { error = "Operation cancelled or exceeded its time limit. Previous source selections remain available." }, 408); }
        catch (ArgumentException e) { await Json(response, new { error = e.Message.Replace(root, "<local session>", StringComparison.OrdinalIgnoreCase) }, 400); }
        catch (Exception e) when (e is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception or HttpListenerException)
        { try { await Json(response, new { error = "Local operation failed. Check the input and FFmpeg/ffprobe installation (9.0.1 validated), then retry." }, 500); } catch (Exception send) when (send is IOException or HttpListenerException or ObjectDisposedException) { } }
        finally { if (locked) gate.Release(); response.Close(); }
    }

    private async Task Upload(HttpListenerRequest request, HttpListenerResponse response)
    {
        var kind = request.QueryString["kind"]; Composition.Require(kind is "video" or "audio", "Choose a video or audio slot.");
        Composition.Require(request.ContentLength64 is > 0 and <= MaxFile, "Each file must be between 1 byte and 50 MiB.");
        var name = Path.GetFileName(Uri.UnescapeDataString(request.Headers["X-File-Name"] ?? ""));
        var extension = Path.GetExtension(name).ToLowerInvariant();
        Composition.Require((kind == "video" ? new[] { ".mp4", ".webm" } : new[] { ".wav", ".mp3" }).Contains(extension), "Video: MP4/WebM. Audio: WAV/MP3.");
        var relative = "uploads/" + Guid.NewGuid().ToString("N") + extension; var target = Path.Combine(root, relative);
        bool accepted = false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using (var stream = File.Create(target))
            {
                var buffer = new byte[65536]; long total = 0; int count;
                while ((count = await request.InputStream.ReadAsync(buffer, timeout.Token)) != 0)
                { total += count; Composition.Require(total <= MaxFile, "File exceeds 50 MiB."); await stream.WriteAsync(buffer.AsMemory(0, count), timeout.Token); }
            }
            var demo = request.QueryString["demo"] ?? "showcase"; Composition.Require(Demos.Contains(demo), "Unknown demo.");
            var c = Composition.Compile(File.ReadAllText(Path.Combine(lab, $"examples/{demo}.json")), lab);
            int frames = kind == "video" ? c.Clips.Select(v => v.Trim + v.Frames).DefaultIfEmpty(1).Max() : c.Script.Audio.Select(a => a.Trim + a.Frames).DefaultIfEmpty(1).Max();
            var info = await AssetProbe.Validate(target, kind!, 0, frames, c.Script.Fps, timeout.Token);
            if (uploads.Remove(kind!, out var old)) File.Delete(Path.Combine(root, old.File));
            uploads[kind!] = (relative, name); cache.Clear(); accepted = true;
            await Json(response, new { name, kind, bytes = new FileInfo(target).Length, duration = info.Duration, width = info.Width, height = info.Height, codec = info.Codec });
        }
        finally { if (!accepted && File.Exists(target)) File.Delete(target); }
    }

    private Snapshot Bind(JsonObject body, out string source)
    {
        var demo = body["demo"]?.GetValue<string>() ?? "showcase"; Composition.Require(Demos.Contains(demo), "Unknown demo.");
        var media = body["media"]?.GetValue<string>() ?? "samples"; Composition.Require(media is "samples" or "personal", "Unknown media source.");
        var video = body["video"]?.GetValue<string>() ?? "original"; var audio = body["audio"]?.GetValue<string>() ?? "original";
        Composition.Require(new[] { "original", "montage", "pattern" }.Contains(video) && new[] { "original", "calm", "pulse" }.Contains(audio), "Unknown sample.");
        var key = $"{demo}|{media}|{video}|{audio}";
        if (!cache.TryGetValue(key, out var item))
        {
            var script = JsonNode.Parse(File.ReadAllText(Path.Combine(lab, $"examples/{demo}.json")))!;
            string Asset(string original, string kind, bool substitute = true)
            {
                if (substitute && media == "personal" && uploads.TryGetValue(kind, out var uploaded)) return uploaded.File;
                var choice = kind == "video" ? video : audio;
                if (substitute && choice != "original") return $"samples/{choice}." + (kind == "video" ? "mp4" : "wav");
                return Path.GetFileName(original) switch { "second.mp4" => "samples/pattern.mp4", "music.wav" => "samples/calm.wav", "first.mp4" => "samples/montage.mp4", var name => "samples/" + name };
            }
            foreach (var clip in script["videos"]!.AsArray()) clip!["asset"] = Asset(clip["asset"]!.GetValue<string>(), "video");
            for (int i = 0; i < script["audio"]!.AsArray().Count; i++) { var clip = script["audio"]![i]!; clip["asset"] = Asset(clip["asset"]!.GetValue<string>(), "audio", i == 0); }
            if (script["data"] is JsonObject data) foreach (var list in data) foreach (var clip in list.Value!.AsArray()) clip!["asset"] = Asset(clip["asset"]!.GetValue<string>(), "video");
            source = script.ToJsonString(); var composition = Composition.Compile(source, root);
            if (cache.Count >= 8) cache.Clear(); item = (composition, composition.CreateRuntime(), source); cache[key] = item;
        }
        source = item.Source;
        var values = body["parameters"]?.Deserialize<Dictionary<string, decimal>>() ?? new();
        var changes = item.Composition.Script.Parameters.ToDictionary(p => p.Key, p => p.Value.Default);
        foreach (var pair in values) changes[pair.Key] = pair.Value;
        item.Runtime.SetMany(changes); return item.Runtime.Bind();
    }

    private static async Task ServeFile(HttpListenerRequest request, HttpListenerResponse response, string file)
    {
        response.ContentType = Path.GetExtension(file) switch { ".html" => "text/html", ".js" => "text/javascript", ".css" => "text/css", ".json" => "application/json", ".mp4" => "video/mp4", ".webm" => "video/webm", ".jpg" => "image/jpeg", ".wav" => "audio/wav", _ => "application/octet-stream" };
        // A reset can remove a result while an existing browser range finishes reading it.
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); long start = 0, end = stream.Length - 1;
        response.Headers["Accept-Ranges"] = "bytes";
        if (request.Headers["Range"] is string header)
        {
            var match = System.Text.RegularExpressions.Regex.Match(header, "^bytes=(\\d+)-(\\d*)$");
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out start) || start > end) { response.StatusCode = 416; return; }
            if (match.Groups[2].Value != "") { if (!long.TryParse(match.Groups[2].Value, out var requested) || requested < start) { response.StatusCode = 416; return; } end = Math.Min(end, requested); }
            response.StatusCode = 206; response.Headers["Content-Range"] = $"bytes {start}-{end}/{stream.Length}";
        }
        response.ContentLength64 = end - start + 1; stream.Position = start; var buffer = new byte[65536]; long remaining = end - start + 1;
        while (remaining > 0) { int count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining))); if (count == 0) break; await response.OutputStream.WriteAsync(buffer.AsMemory(0, count)); remaining -= count; }
    }
}
